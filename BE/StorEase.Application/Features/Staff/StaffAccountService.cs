using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Staff.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Staff;

public interface IStaffAccountService
{
    Task<Result<PagedResult<StaffAccountDto>>> ListAsync(
        string? role, UserStatus? status, int page, int pageSize, CancellationToken ct);
    Task<Result<StaffAccountDto>> GetAsync(int userId, CancellationToken ct);
    Task<Result<StaffAccountDto>> CreateAsync(CreateStaffAccountRequest request, CancellationToken ct);
    Task<Result<StaffAccountDto>> UpdateAsync(int userId, UpdateStaffAccountRequest request, CancellationToken ct);
    Task<Result<StaffAccountDto>> SetFacilitiesAsync(int userId, SetStaffFacilitiesRequest request, CancellationToken ct);
}

public class StaffAccountService(IUnitOfWork uow, ICurrentUser currentUser, IPasswordHasher hasher)
    : IStaffAccountService
{
    public async Task<Result<PagedResult<StaffAccountDto>>> ListAsync(
        string? role, UserStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var q = StaffUsers();
        if (!string.IsNullOrWhiteSpace(role))
            q = q.Where(u => u.Role.RoleName == role);
        if (status is not null)
            q = q.Where(u => u.Status == status);

        return await Project(q.OrderBy(u => u.FullName).ThenBy(u => u.UserId))
            .ToPagedResultAsync(page, pageSize, ct);
    }

    public async Task<Result<StaffAccountDto>> GetAsync(int userId, CancellationToken ct)
    {
        var dto = await Project(StaffUsers().Where(u => u.UserId == userId)).FirstOrDefaultAsync(ct);
        return dto is null ? Error.NotFound("Staff account not found.") : dto;
    }

    public async Task<Result<StaffAccountDto>> CreateAsync(CreateStaffAccountRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();
        var taken = await uow.Repository<User>().Query()
            .AnyAsync(u => u.Email.ToLower() == email.ToLower(), ct);
        if (taken)
            return Error.Conflict("An account with this email already exists.");

        var role = await uow.Repository<Role>().Query().FirstOrDefaultAsync(r => r.RoleName == request.Role, ct);
        // The Role table is filled by the seeder; a missing row is a setup problem, reported as not found.
        if (role is null)
            return Error.NotFound($"Role '{request.Role}' does not exist.");

        var user = new User
        {
            RoleId = role.RoleId,
            FullName = request.FullName.Trim(),
            Email = email,
            PhoneNumber = request.PhoneNumber.Trim(),
            PasswordHash = hasher.Hash(request.Password),
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
        };
        uow.Repository<User>().Add(user);
        await uow.SaveChangesAsync(ct);
        return await GetAsync(user.UserId, ct);
    }

    public async Task<Result<StaffAccountDto>> UpdateAsync(
        int userId, UpdateStaffAccountRequest request, CancellationToken ct)
    {
        var changed = await uow.InTransactionAsync<int>(async () =>
        {
            var user = await StaffUsers().Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == userId, ct);
            if (user is null)
                return Error.NotFound("Staff account not found.");

            // An admin who locks or demotes themselves could leave nobody able to undo it.
            if (userId == currentUser.UserId)
            {
                if (request.Status is { } s && s != UserStatus.Active)
                    return Error.Conflict("You cannot deactivate your own account.");
                if (request.Role is { } r && r != user.Role.RoleName)
                    return Error.Conflict("You cannot change your own role.");
            }

            if (request.Role is { } newRole && newRole != user.Role.RoleName)
            {
                var role = await uow.Repository<Role>().Query().FirstOrDefaultAsync(r => r.RoleName == newRole, ct);
                if (role is null)
                    return Error.NotFound($"Role '{newRole}' does not exist.");
                user.Role = role;
            }
            if (request.FullName is not null) user.FullName = request.FullName.Trim();
            if (request.PhoneNumber is not null) user.PhoneNumber = request.PhoneNumber.Trim();
            if (request.Status is not null) user.Status = request.Status.Value;
            return userId;
        }, ct);
        return changed.IsSuccess ? await GetAsync(userId, ct) : changed.Error;
    }

    public async Task<Result<StaffAccountDto>> SetFacilitiesAsync(
        int userId, SetStaffFacilitiesRequest request, CancellationToken ct)
    {
        var wanted = request.FacilityIds.Distinct().ToList();
        var done = await uow.InTransactionAsync<int>(async () =>
        {
            if (!await StaffUsers().AnyAsync(u => u.UserId == userId, ct))
                return Error.NotFound("Staff account not found.");

            var found = await uow.Repository<Facility>().Query()
                .Where(f => wanted.Contains(f.FacilityId)).CountAsync(ct);
            if (found != wanted.Count)
                return Error.NotFound("One or more facilities were not found.");

            var current = await uow.Repository<StaffFacility>().Query()
                .Where(s => s.UserId == userId).ToListAsync(ct);

            // A facility manager may add or remove only the facilities they manage; the staff member's others stay.
            if (currentUser.Role == Roles.FacilityManager)
            {
                if (userId == currentUser.UserId)
                    return Error.Forbidden("Only a system administrator can change your own facilities.");
                var mine = await StaffScope.FacilityIdsOf(uow, currentUser.UserId).ToListAsync(ct);
                var currentIds = current.Select(r => r.FacilityId).ToList();
                if (wanted.Except(currentIds).Concat(currentIds.Except(wanted)).Any(id => !mine.Contains(id)))
                    return Error.Forbidden("You can only assign staff to facilities you manage.");
            }

            // Unassigning must not strand work: refuse while the person still has open work at that facility.
            var leaving = current.Where(r => !wanted.Contains(r.FacilityId)).Select(r => r.FacilityId).ToList();
            if (leaving.Count > 0 && await OpenWorkAsync(userId, leaving, ct) is { } blocker)
                return blocker;

            // StaffFacility is a link, not history, so unassigning deletes the row (design Q4).
            foreach (var row in current.Where(r => !wanted.Contains(r.FacilityId)))
                uow.Repository<StaffFacility>().Remove(row);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            foreach (var facilityId in wanted.Except(current.Select(r => r.FacilityId)))
                uow.Repository<StaffFacility>().Add(
                    new StaffFacility { UserId = userId, FacilityId = facilityId, AssignedDate = today });
            return userId;
        }, ct);
        return done.IsSuccess ? await GetAsync(userId, ct) : done.Error;
    }

    /// <summary>Describes open work that ties <paramref name="userId"/> to one of the facilities, or null if none.</summary>
    private async Task<Error?> OpenWorkAsync(int userId, List<int> facilityIds, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var shifts = await uow.Repository<Shift>().Query()
            .CountAsync(s => s.UserId == userId && facilityIds.Contains(s.FacilityId) && s.ShiftDate >= today, ct);
        var tickets = await uow.Repository<SupportTicket>().Query()
            .CountAsync(t => t.AssignedTo == userId && facilityIds.Contains(t.FacilityId) &&
                             t.Status != SupportTicketStatus.Resolved && t.Status != SupportTicketStatus.Closed, ct);
        var tasks = await uow.Repository<StaffTask>().Query()
            .CountAsync(t => t.AssignedTo == userId && t.Shift != null && facilityIds.Contains(t.Shift.FacilityId) &&
                             (t.Status == StaffTaskStatus.ToDo || t.Status == StaffTaskStatus.InProgress), ct);
        var orders = await uow.Repository<MaintenanceOrder>().Query()
            .CountAsync(o => o.AssignedTo == userId && facilityIds.Contains(o.StorageUnit.FacilityId) &&
                             (o.Status == MaintenanceOrderStatus.Open || o.Status == MaintenanceOrderStatus.Scheduled ||
                              o.Status == MaintenanceOrderStatus.InProgress), ct);
        if (shifts + tickets + tasks + orders == 0)
            return null;
        return Error.Conflict($"Cannot unassign a facility with open work: {shifts} upcoming shift(s), {tickets} open ticket(s), " +
                              $"{tasks} open task(s), {orders} open maintenance order(s). Reassign or finish them first.");
    }

    // Customers are not managed here; this service only sees staff roles, and a facility manager facility staff and
    // themselves (so the portal knows which facilities they manage).
    private IQueryable<User> StaffUsers()
    {
        var userId = currentUser.UserId;
        return currentUser.Role == Roles.FacilityManager
            ? uow.Repository<User>().Query().Where(u => u.Role.RoleName == Roles.FacilityStaff || u.UserId == userId)
            : uow.Repository<User>().Query().Where(u => u.Role.RoleName != Roles.StorageCustomer);
    }

    private IQueryable<StaffAccountDto> Project(IQueryable<User> q) => q.Select(u => new StaffAccountDto(
        u.UserId, u.FullName, u.Email, u.PhoneNumber, u.Role.RoleName, u.Status, u.CreatedAt,
        uow.Repository<StaffFacility>().Query()
            .Where(s => s.UserId == u.UserId)
            .OrderBy(s => s.Facility.Name)
            .Select(s => new StaffFacilityDto(s.FacilityId, s.Facility.Name))
            .ToList()));
}
