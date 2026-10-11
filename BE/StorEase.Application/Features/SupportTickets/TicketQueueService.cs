using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.SupportTickets.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.SupportTickets;

public interface ITicketQueueService
{
    Task<Result<PagedResult<TicketQueueItemDto>>> ListAsync(
        SupportTicketStatus? status, Priority? priority, int? facilityId, bool assignedToMe,
        int page, int pageSize, CancellationToken ct);
    Task<Result<TicketQueueItemDto>> AssignAsync(int ticketId, AssignTicketRequest request, CancellationToken ct);
    Task<Result<TicketQueueItemDto>> ChangeStatusAsync(int ticketId, ChangeTicketStatusRequest request, CancellationToken ct);
}

public class TicketQueueService(IUnitOfWork uow, ICurrentUser currentUser) : ITicketQueueService
{
    public async Task<Result<PagedResult<TicketQueueItemDto>>> ListAsync(
        SupportTicketStatus? status, Priority? priority, int? facilityId, bool assignedToMe,
        int page, int pageSize, CancellationToken ct)
    {
        var query = Visible();
        if (status is not null)
            query = query.Where(t => t.Status == status);
        if (priority is not null)
            query = query.Where(t => t.Priority == priority);
        if (facilityId is not null)
            query = query.Where(t => t.FacilityId == facilityId);
        if (assignedToMe)
        {
            var me = currentUser.UserId;
            query = query.Where(t => t.AssignedTo == me);
        }

        // Priority is stored as a string, so ordering by the column would sort alphabetically (High < Low < Normal < Urgent).
        var ordered = query
            .OrderByDescending(t => t.Priority == Priority.Urgent ? 3
                : t.Priority == Priority.High ? 2
                : t.Priority == Priority.Normal ? 1 : 0)
            .ThenBy(t => t.CreatedAt)
            .ThenBy(t => t.TicketId);

        return await Project(ordered).ToPagedResultAsync(page, pageSize, ct);
    }

    public async Task<Result<TicketQueueItemDto>> AssignAsync(
        int ticketId, AssignTicketRequest request, CancellationToken ct)
    {
        var done = await uow.InTransactionAsync<int>(async () =>
        {
            var ticket = await Visible().FirstOrDefaultAsync(t => t.TicketId == ticketId, ct);
            if (ticket is null)
                return Error.NotFound("Ticket not found.");
            if (ticket.Status is SupportTicketStatus.Resolved or SupportTicketStatus.Closed)
                return Error.Conflict($"A {ticket.Status} ticket cannot be assigned.");

            var assignee = await uow.Repository<User>().Query()
                .Where(u => u.UserId == request.AssigneeId)
                .Select(u => new { u.Status, RoleName = u.Role.RoleName })
                .FirstOrDefaultAsync(ct);
            if (assignee is null)
                return Error.NotFound("Assignee not found.");
            if (assignee.Status != UserStatus.Active ||
                assignee.RoleName is not (Roles.FacilityStaff or Roles.FacilityManager))
                return Error.Conflict("The assignee must be an active facility staff member or manager.");

            // A manager runs the facility through Facility.ManagerId and may have no StaffFacility row.
            var worksHere = await StaffScope.FacilityIdsOf(uow, request.AssigneeId)
                .AnyAsync(id => id == ticket.FacilityId, ct);
            if (!worksHere)
                return Error.Conflict("The assignee is not assigned to this ticket's facility.");

            ticket.AssignedTo = request.AssigneeId;
            ticket.DueDate = request.DueDate is { } due ? VietnamTime.ToUtc(due) : null;
            if (ticket.Status == SupportTicketStatus.New)
                ticket.Status = SupportTicketStatus.InProgress;
            return ticket.TicketId;
        }, ct);

        return done.IsSuccess ? await GetAsync(done.Value, ct) : done.Error;
    }

    public async Task<Result<TicketQueueItemDto>> ChangeStatusAsync(
        int ticketId, ChangeTicketStatusRequest request, CancellationToken ct)
    {
        var done = await uow.InTransactionAsync<int>(async () =>
        {
            var ticket = await Visible().FirstOrDefaultAsync(t => t.TicketId == ticketId, ct);
            if (ticket is null)
                return Error.NotFound("Ticket not found.");

            var target = request.Status;
            if (TicketStatusRules.CheckMove(currentUser.Role, currentUser.IsCustomer, ticket.Status, target) is { } refused)
                return refused;

            ticket.Status = target;
            if (target == SupportTicketStatus.Resolved)
                ticket.ResolvedAt = DateTime.UtcNow;
            return ticket.TicketId;
        }, ct);

        return done.IsSuccess ? await GetAsync(done.Value, ct) : done.Error;
    }

    private async Task<Result<TicketQueueItemDto>> GetAsync(int ticketId, CancellationToken ct)
    {
        var dto = await Project(Visible().Where(t => t.TicketId == ticketId)).FirstOrDefaultAsync(ct);
        return dto is null ? Error.NotFound("Ticket not found.") : dto;
    }

    // Other people's tickets are 404, not 403, so ids cannot be probed.
    private IQueryable<SupportTicket> Visible()
    {
        var query = uow.Repository<SupportTicket>().Query();
        var userId = currentUser.UserId;
        if (currentUser.IsCustomer)
            return query.Where(t => t.CustomerId == userId);
        if (StaffScope.SeesAllFacilities(currentUser.Role))
            return query;

        var facilityIds = StaffScope.FacilityIdsOf(uow, userId);
        return query.Where(t => facilityIds.Contains(t.FacilityId));
    }

    private static IQueryable<TicketQueueItemDto> Project(IQueryable<SupportTicket> source) =>
        source.Select(t => new TicketQueueItemDto(
            t.TicketId,
            t.TicketCode,
            t.Subject,
            t.Priority,
            t.Status,
            t.CategoryId,
            t.Category.Name,
            t.CustomerId,
            t.Customer.FullName,
            t.FacilityId,
            t.Facility.Name,
            t.StorageUnitId,
            t.StorageUnit != null ? t.StorageUnit.UnitCode : null,
            t.AssignedTo,
            t.AssignedToUser != null ? t.AssignedToUser.FullName : null,
            t.DueDate,
            t.CreatedAt,
            t.ResolvedAt));
}
