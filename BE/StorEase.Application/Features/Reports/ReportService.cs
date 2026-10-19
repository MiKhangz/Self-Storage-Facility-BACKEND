using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Reports;

public sealed record DashboardDto(
    int TotalUnits, int AvailableUnits, int ReservedUnits, int OccupiedUnits, int MaintenanceUnits, decimal OccupancyRate,
    int ActiveContracts, int UpcomingMoveIns, int OpenOverdueCases, decimal OutstandingAmount,
    decimal RevenueThisMonth, int OpenTickets);

public sealed record OccupancyRowDto(
    int FacilityId, string FacilityName, int UnitTypeId, string UnitTypeCode,
    int Total, int Available, int Reserved, int Occupied, int Maintenance, decimal OccupancyRate);

public sealed record RevenueMonthDto(string Month, decimal Collected, decimal Refunded, decimal Net, int Payments);

public sealed record RevenueMethodDto(PaymentMethod Method, decimal Collected, int Payments);

public sealed record RevenueReportDto(
    DateOnly From, DateOnly To, decimal Collected, decimal Refunded, decimal Net,
    IReadOnlyList<RevenueMonthDto> Months, IReadOnlyList<RevenueMethodDto> Methods);

public interface IReportService
{
    Task<Result<DashboardDto>> DashboardAsync(int? facilityId, CancellationToken ct);
    Task<Result<IReadOnlyList<OccupancyRowDto>>> OccupancyAsync(int? facilityId, CancellationToken ct);
    Task<Result<RevenueReportDto>> RevenueAsync(DateOnly from, DateOnly to, int? facilityId, CancellationToken ct);
}

/// <summary>
/// Read-only figures for the dashboards. A facility manager sees their facilities; Business Operations and
/// administrators see every facility, or one when <c>facilityId</c> is given.
/// </summary>
public sealed class ReportService(IUnitOfWork uow, ICurrentUser currentUser) : IReportService
{
    public async Task<Result<DashboardDto>> DashboardAsync(int? facilityId, CancellationToken ct)
    {
        var scope = Scope(facilityId);
        var units = await uow.Repository<StorageUnit>().Query().Where(u => scope.Contains(u.FacilityId))
            .GroupBy(u => u.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        int Count(StorageUnitStatus s) => units.FirstOrDefault(u => u.Status == s)?.Count ?? 0;

        var today = VietnamTime.Today;
        var monthStart = VietnamTime.ToUtc(new DateTime(today.Year, today.Month, 1));
        var contracts = uow.Repository<Contract>().Query().Where(c => scope.Contains(c.StorageUnit.FacilityId));
        var activeContracts = await contracts.CountAsync(c => c.Status == ContractStatus.Active
            || c.Status == ContractStatus.Overdue || c.Status == ContractStatus.Suspended || c.Status == ContractStatus.PendingHandover, ct);
        var upcoming = await uow.Repository<Reservation>().Query().CountAsync(r => scope.Contains(r.FacilityId)
            && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed)
            && r.MoveInDate >= today && r.MoveInDate <= today.AddDays(7), ct);
        var openCases = await uow.Repository<OverdueCase>().Query()
            .CountAsync(c => c.ResolvedAt == null && scope.Contains(c.Contract.StorageUnit.FacilityId), ct);
        var invoices = InScope(uow.Repository<Invoice>().Query(), scope);
        var outstanding = await invoices.Where(i => i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Overdue)
            .Select(i => i.TotalAmount - i.PaidAmount).ToListAsync(ct);
        var revenue = await uow.Repository<Payment>().Query()
            .Where(p => p.Status == PaymentStatus.Succeeded && p.PaidAt >= monthStart && invoices.Select(i => i.InvoiceId).Contains(p.InvoiceId))
            .Select(p => p.Amount).ToListAsync(ct);
        var openTickets = await uow.Repository<SupportTicket>().Query().CountAsync(t => scope.Contains(t.FacilityId)
            && t.Status != SupportTicketStatus.Resolved && t.Status != SupportTicketStatus.Closed, ct);

        var total = units.Where(u => u.Status != StorageUnitStatus.Retired).Sum(u => u.Count);
        return new DashboardDto(total, Count(StorageUnitStatus.Available), Count(StorageUnitStatus.Reserved),
            Count(StorageUnitStatus.Occupied), Count(StorageUnitStatus.Maintenance), Rate(Count(StorageUnitStatus.Occupied), total),
            activeContracts, upcoming, openCases, Money.Round(outstanding.Sum()), Money.Round(revenue.Sum()), openTickets);
    }

    public async Task<Result<IReadOnlyList<OccupancyRowDto>>> OccupancyAsync(int? facilityId, CancellationToken ct)
    {
        var scope = Scope(facilityId);
        var rows = await uow.Repository<StorageUnit>().Query()
            .Where(u => scope.Contains(u.FacilityId) && u.Status != StorageUnitStatus.Retired)
            .GroupBy(u => new { u.FacilityId, u.Facility.Name, u.UnitTypeId, u.UnitType.Code, u.Status })
            .Select(g => new { g.Key.FacilityId, g.Key.Name, g.Key.UnitTypeId, g.Key.Code, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        return rows.GroupBy(r => new { r.FacilityId, r.Name, r.UnitTypeId, r.Code })
            .Select(g =>
            {
                int Of(StorageUnitStatus s) => g.Where(r => r.Status == s).Sum(r => r.Count);
                var total = g.Sum(r => r.Count);
                return new OccupancyRowDto(g.Key.FacilityId, g.Key.Name, g.Key.UnitTypeId, g.Key.Code, total,
                    Of(StorageUnitStatus.Available), Of(StorageUnitStatus.Reserved), Of(StorageUnitStatus.Occupied),
                    Of(StorageUnitStatus.Maintenance), Rate(Of(StorageUnitStatus.Occupied), total));
            })
            .OrderBy(r => r.FacilityName).ThenBy(r => r.UnitTypeCode).ToList();
    }

    public async Task<Result<RevenueReportDto>> RevenueAsync(DateOnly from, DateOnly to, int? facilityId, CancellationToken ct)
    {
        if (to < from)
            return Error.Validation("'to' must be on or after 'from'.");
        if (to.DayNumber - from.DayNumber > 366 * 3)
            return Error.Validation("The range cannot exceed three years.");

        var scope = Scope(facilityId);
        var start = VietnamTime.ToUtc(from.ToDateTime(TimeOnly.MinValue));
        var end = VietnamTime.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var invoiceIds = InScope(uow.Repository<Invoice>().Query(), scope).Select(i => i.InvoiceId);
        // Refunded payments were collected first, so they count in both columns.
        var payments = await uow.Repository<Payment>().Query()
            .Where(p => (p.Status == PaymentStatus.Succeeded || p.Status == PaymentStatus.Refunded)
                && p.PaidAt >= start && p.PaidAt < end && invoiceIds.Contains(p.InvoiceId))
            .Select(p => new { p.PaidAt, p.Amount, p.Method, p.Status })
            .ToListAsync(ct);

        var local = payments.Select(p => new
        {
            Month = (p.PaidAt!.Value + TimeSpan.FromHours(7)).ToString("yyyy-MM"),
            p.Amount, p.Method, Refunded = p.Status == PaymentStatus.Refunded,
        }).ToList();
        var months = local.GroupBy(p => p.Month).OrderBy(g => g.Key).Select(g =>
        {
            var collected = g.Sum(p => p.Amount);
            var refunded = g.Where(p => p.Refunded).Sum(p => p.Amount);
            return new RevenueMonthDto(g.Key, Money.Round(collected), Money.Round(refunded), Money.Round(collected - refunded), g.Count());
        }).ToList();
        var methods = local.GroupBy(p => p.Method).OrderBy(g => g.Key)
            .Select(g => new RevenueMethodDto(g.Key, Money.Round(g.Sum(p => p.Amount)), g.Count())).ToList();

        var totalCollected = months.Sum(m => m.Collected);
        var totalRefunded = months.Sum(m => m.Refunded);
        return new RevenueReportDto(from, to, totalCollected, totalRefunded, Money.Round(totalCollected - totalRefunded), months, methods);
    }

    private IQueryable<int> Scope(int? facilityId)
    {
        var facilities = uow.Repository<Facility>().Query().Select(f => f.FacilityId);
        if (!StaffScope.SeesAllFacilities(currentUser.Role))
        {
            var mine = StaffScope.FacilityIdsOf(uow, currentUser.UserId);
            facilities = facilities.Where(id => mine.Contains(id));
        }
        return facilityId is null ? facilities : facilities.Where(id => id == facilityId);
    }

    private static IQueryable<Invoice> InScope(IQueryable<Invoice> invoices, IQueryable<int> scope) => invoices.Where(i =>
        (i.Contract != null && scope.Contains(i.Contract.StorageUnit.FacilityId))
        || (i.Contract == null && i.Reservation != null && scope.Contains(i.Reservation.FacilityId)));

    private static decimal Rate(int part, int total) => total == 0 ? 0 : Math.Round(part * 100m / total, 2);
}
