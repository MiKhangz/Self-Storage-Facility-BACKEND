using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Maintenance.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Maintenance;

public interface IMaintenanceService
{
    Task<Result<MaintenanceOrderDto>> CreateAsync(CreateMaintenanceOrderRequest request, CancellationToken ct);
    Task<Result<MaintenanceOrderDto>> GetAsync(int orderId, CancellationToken ct);
    Task<Result<PagedResult<MaintenanceOrderDto>>> ListAsync(
        MaintenanceOrderStatus? status, int? storageUnitId, int page, int pageSize, CancellationToken ct);
    Task<Result<MaintenanceOrderDto>> ChangeStatusAsync(int orderId, ChangeMaintenanceStatusRequest request, CancellationToken ct);
}

public class MaintenanceService(IUnitOfWork uow, ICurrentUser currentUser) : IMaintenanceService
{
    private static readonly MaintenanceOrderStatus[] LiveStatuses =
        [MaintenanceOrderStatus.Open, MaintenanceOrderStatus.Scheduled, MaintenanceOrderStatus.InProgress];

    public async Task<Result<MaintenanceOrderDto>> CreateAsync(CreateMaintenanceOrderRequest request, CancellationToken ct)
    {
        var scope = Scope();
        var unitId = request.StorageUnitId;
        if (request.TicketId is { } ticketId)
        {
            var ticket = await uow.Repository<SupportTicket>().Query()
                .Where(t => t.TicketId == ticketId && (scope == null || scope.Contains(t.FacilityId)))
                .Select(t => new { t.StorageUnitId })
                .FirstOrDefaultAsync(ct);
            if (ticket is null)
                return Error.NotFound("Ticket not found.");
            if (ticket.StorageUnitId is null)
                return Error.Validation("The ticket is not about a storage unit, so it cannot lead to a maintenance order.");
            if (unitId is not null && unitId != ticket.StorageUnitId)
                return Error.Validation("The storage unit does not match the ticket.");
            unitId = ticket.StorageUnitId;
        }

        var unit = await uow.Repository<StorageUnit>().Query()
            .Where(u => u.StorageUnitId == unitId && (scope == null || scope.Contains(u.FacilityId)))
            .Select(u => new { u.StorageUnitId, u.FacilityId })
            .FirstOrDefaultAsync(ct);
        if (unit is null)
            return Error.NotFound("Storage unit not found.");

        if (request.AssignedTo is { } assignee)
        {
            var isStaffHere = await uow.Repository<User>().Query().AnyAsync(u =>
                u.UserId == assignee && u.Status == UserStatus.Active &&
                (u.Role.RoleName == Roles.FacilityStaff || u.Role.RoleName == Roles.FacilityManager) &&
                (uow.Repository<StaffFacility>().Query().Any(s => s.UserId == assignee && s.FacilityId == unit.FacilityId) ||
                 uow.Repository<Facility>().Query().Any(f => f.FacilityId == unit.FacilityId && f.ManagerId == assignee)), ct);
            if (!isStaffHere)
                return Error.Validation("The assignee must be an active staff member of this facility.");
        }

        var order = new MaintenanceOrder
        {
            StorageUnitId = unit.StorageUnitId,
            TicketId = request.TicketId,
            AssignedTo = request.AssignedTo,
            Description = request.Description.Trim(),
            ScheduledAt = request.ScheduledAt is { } at ? VietnamTime.ToUtc(at) : null,
            Status = request.ScheduledAt is null ? MaintenanceOrderStatus.Open : MaintenanceOrderStatus.Scheduled,
        };
        uow.Repository<MaintenanceOrder>().Add(order);
        await uow.SaveChangesAsync(ct);

        return await GetAsync(order.MaintenanceOrderId, ct);
    }

    public async Task<Result<MaintenanceOrderDto>> GetAsync(int orderId, CancellationToken ct)
    {
        var dto = await Project(Visible().Where(o => o.MaintenanceOrderId == orderId)).FirstOrDefaultAsync(ct);
        // An order outside the caller's facilities is a 404, so ids cannot be probed.
        return dto is null ? Error.NotFound("Maintenance order not found.") : dto;
    }

    public async Task<Result<PagedResult<MaintenanceOrderDto>>> ListAsync(
        MaintenanceOrderStatus? status, int? storageUnitId, int page, int pageSize, CancellationToken ct)
    {
        var query = Visible();
        if (status is not null)
            query = query.Where(o => o.Status == status);
        if (storageUnitId is not null)
            query = query.Where(o => o.StorageUnitId == storageUnitId);

        return await Project(query.OrderByDescending(o => o.MaintenanceOrderId)).ToPagedResultAsync(page, pageSize, ct);
    }

    public async Task<Result<MaintenanceOrderDto>> ChangeStatusAsync(
        int orderId, ChangeMaintenanceStatusRequest request, CancellationToken ct)
    {
        var changed = await uow.InTransactionAsync<int>(async () =>
        {
            var order = await Visible().Include(o => o.StorageUnit)
                .FirstOrDefaultAsync(o => o.MaintenanceOrderId == orderId, ct);
            if (order is null)
                return Error.NotFound("Maintenance order not found.");

            var target = request.Status;
            var allowed = target switch
            {
                MaintenanceOrderStatus.Scheduled or MaintenanceOrderStatus.InProgress =>
                    order.Status is MaintenanceOrderStatus.Open or MaintenanceOrderStatus.Scheduled,
                MaintenanceOrderStatus.Done => order.Status == MaintenanceOrderStatus.InProgress,
                MaintenanceOrderStatus.Cancelled => LiveStatuses.Contains(order.Status),
                _ => false,
            };
            if (!allowed)
                return Error.Conflict($"A {order.Status} order cannot move to {target}.");

            var unit = order.StorageUnit;
            // Only work that was actually under way took the unit off the market; closing an order that
            // never started must not release a unit held in Maintenance for another reason (e.g. return damage).
            var wasUnderWay = order.Status == MaintenanceOrderStatus.InProgress;
            switch (target)
            {
                case MaintenanceOrderStatus.Scheduled:
                    order.ScheduledAt = VietnamTime.ToUtc(request.ScheduledAt!.Value);
                    break;
                case MaintenanceOrderStatus.InProgress:
                    // A Reserved or Occupied unit keeps its status; only a free unit is taken off the market.
                    if (unit.Status == StorageUnitStatus.Available)
                        unit.Status = StorageUnitStatus.Maintenance;
                    break;
                case MaintenanceOrderStatus.Done:
                    order.CompletedAt = DateTime.UtcNow;
                    order.Cost = request.Cost;
                    order.WorkNote = request.WorkNote!.Trim();
                    break;
            }
            order.Status = target;

            if (target is MaintenanceOrderStatus.Done or MaintenanceOrderStatus.Cancelled && wasUnderWay &&
                unit.Status == StorageUnitStatus.Maintenance)
            {
                var otherLive = await uow.Repository<MaintenanceOrder>().Query().AnyAsync(o =>
                    o.StorageUnitId == unit.StorageUnitId &&
                    o.MaintenanceOrderId != order.MaintenanceOrderId &&
                    LiveStatuses.Contains(o.Status), ct);
                // A repair finished on a floor that is closed leaves the unit off the market until the floor reopens.
                var floorOpen = await uow.Repository<Floor>().Query().AnyAsync(f => f.FloorId == unit.FloorId && f.Status == FloorStatus.Open, ct);
                if (!otherLive && floorOpen)
                    unit.Status = StorageUnitStatus.Available;
            }

            await uow.SaveChangesAsync(ct);
            return order.MaintenanceOrderId;
        }, ct);

        return changed.IsSuccess ? await GetAsync(orderId, ct) : changed.Error;
    }

    /// <summary>Null means every facility; otherwise a subquery of the caller's facility ids.</summary>
    private IQueryable<int>? Scope() =>
        StaffScope.SeesAllFacilities(currentUser.Role) ? null : StaffScope.FacilityIdsOf(uow, currentUser.UserId);

    private IQueryable<MaintenanceOrder> Visible()
    {
        var scope = Scope();
        var query = uow.Repository<MaintenanceOrder>().Query();
        return scope is null ? query : query.Where(o => scope.Contains(o.StorageUnit.FacilityId));
    }

    private static IQueryable<MaintenanceOrderDto> Project(IQueryable<MaintenanceOrder> query) =>
        query.Select(o => new MaintenanceOrderDto(
            o.MaintenanceOrderId, o.StorageUnitId, o.StorageUnit.UnitCode, o.StorageUnit.FacilityId,
            o.TicketId, o.AssignedTo, o.Description, o.ScheduledAt, o.CompletedAt, o.Cost, o.WorkNote, o.Status));
}
