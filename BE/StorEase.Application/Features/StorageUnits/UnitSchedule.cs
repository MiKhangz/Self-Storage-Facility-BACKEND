using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.StorageUnits;

/// <summary>
/// The one rule for "is this unit free from one date to another". A unit is busy while a contract holds it (from its
/// start until its end date, or until today when the renter is still inside after the end date) and while a booking
/// holds it (from its move-in date for the months booked). Booking, staff assignment, renewals and the daily job all
/// use it, so a unit is never promised twice for the same days.
/// </summary>
public static class UnitSchedule
{
    /// <summary>Contracts that hold their unit; a Closed or Terminated one has let it go.</summary>
    private static readonly ContractStatus[] Holding =
    [
        ContractStatus.Draft, ContractStatus.Active, ContractStatus.PendingReturn, ContractStatus.Overdue,
        ContractStatus.Suspended, ContractStatus.Overstay,
    ];

    /// <summary>
    /// Contracts still on their unit. One at a time: the index allows only one Active, Draft or Overstay, and the others
    /// can return to those (a payment, the end date), which would fail that save.
    /// </summary>
    public static IQueryable<Contract> HoldingContracts(IUnitOfWork uow) =>
        uow.Repository<Contract>().Query().Where(c => Holding.Contains(c.Status));

    /// <summary>The last day a booking covers.</summary>
    public static DateOnly EndOf(DateOnly moveIn, int months) => moveIn.AddMonths(months).AddDays(-1);

    /// <summary>Units of that size at that facility free for every day from <paramref name="from"/> to <paramref name="to"/>, by unit code.</summary>
    public static async Task<List<StorageUnit>> FreeUnitsAsync(
        IUnitOfWork uow, int facilityId, int unitTypeId, DateOnly from, DateOnly to, CancellationToken ct,
        int? exceptReservationId = null, int? exceptContractId = null)
    {
        var units = await uow.Repository<StorageUnit>().Query()
            .Where(u => u.FacilityId == facilityId && u.UnitTypeId == unitTypeId
                && u.Status != StorageUnitStatus.Retired && u.Status != StorageUnitStatus.Maintenance)
            .OrderBy(u => u.UnitCode)
            .ToListAsync(ct);
        var busy = await BusyAsync(uow, units.Select(u => u.StorageUnitId).ToList(), from, to, exceptReservationId, exceptContractId, ct);
        return units.Where(u => !busy.Contains(u.StorageUnitId)).ToList();
    }

    public static async Task<bool> IsFreeAsync(
        IUnitOfWork uow, int storageUnitId, DateOnly from, DateOnly to, CancellationToken ct,
        int? exceptReservationId = null, int? exceptContractId = null) =>
        await uow.Repository<StorageUnit>().Query().AnyAsync(u => u.StorageUnitId == storageUnitId
            && u.Status != StorageUnitStatus.Retired && u.Status != StorageUnitStatus.Maintenance, ct)
        && !(await BusyAsync(uow, [storageUnitId], from, to, exceptReservationId, exceptContractId, ct)).Contains(storageUnitId);

    /// <summary>
    /// Bookings of that size and facility overlapping the period that hold no unit yet (made before units were held at
    /// booking). Each still needs a unit, so they use up one free unit each.
    /// </summary>
    public static async Task<int> UnplacedBookingsAsync(
        IUnitOfWork uow, int facilityId, int unitTypeId, DateOnly from, DateOnly to, CancellationToken ct, int? exceptReservationId = null)
    {
        var bookings = await uow.Repository<Reservation>().Query()
            .Where(r => r.FacilityId == facilityId && r.UnitTypeId == unitTypeId && r.StorageUnitId == null
                && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed)
                && r.MoveInDate <= to && r.ReservationId != (exceptReservationId ?? 0))
            .Select(r => new { r.MoveInDate, r.RentalMonths })
            .ToListAsync(ct);
        return bookings.Count(b => EndOf(b.MoveInDate, b.RentalMonths) >= from);
    }

    /// <summary>A unit back from repair or retirement: waiting for the booking that holds it, else on sale.</summary>
    public static Task PutBackAsync(IUnitOfWork uow, StorageUnit unit, CancellationToken ct)
    {
        unit.Status = StorageUnitStatus.Available;
        return ReleaseAsync(uow, unit, ct);
    }

    /// <summary>
    /// After a contract lets its unit go: the unit waits for the next booking that holds it (Reserved), else goes back
    /// on sale (Available). Units under maintenance or retired keep their status.
    /// </summary>
    public static async Task ReleaseAsync(IUnitOfWork uow, StorageUnit unit, CancellationToken ct)
    {
        if (unit.Status is StorageUnitStatus.Maintenance or StorageUnitStatus.Retired)
            return;
        var heldNext = await uow.Repository<Reservation>().Query().AnyAsync(r => r.StorageUnitId == unit.StorageUnitId
            && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed), ct);
        unit.Status = heldNext ? StorageUnitStatus.Reserved : StorageUnitStatus.Available;
    }

    private static async Task<HashSet<int>> BusyAsync(
        IUnitOfWork uow, List<int> unitIds, DateOnly from, DateOnly to, int? exceptReservationId, int? exceptContractId, CancellationToken ct)
    {
        if (unitIds.Count == 0)
            return [];
        var today = VietnamTime.Today;
        // shortcut: the overlap is finished in memory (a facility has tens of units), which keeps the query
        // portable across SQL Server and the SQLite tests; move it into SQL if a facility ever holds thousands.
        var contracts = await uow.Repository<Contract>().Query()
            .Where(c => unitIds.Contains(c.StorageUnitId) && Holding.Contains(c.Status) && c.StartDate <= to
                && c.ContractId != (exceptContractId ?? 0)
                // A booking's own contract never blocks that booking.
                && c.ReservationId != (exceptReservationId ?? 0))
            .Select(c => new { c.StorageUnitId, c.EndDate, c.Status })
            .ToListAsync(ct);
        // A renter past the end date holds the unit until they leave, so nobody can count on it.
        var busy = contracts.Where(c => c.Status == ContractStatus.Overstay || (c.EndDate > today ? c.EndDate : today) >= from)
            .Select(c => c.StorageUnitId).ToHashSet();

        var bookings = await uow.Repository<Reservation>().Query()
            .Where(r => r.StorageUnitId != null && unitIds.Contains(r.StorageUnitId.Value)
                && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed)
                && r.MoveInDate <= to && r.ReservationId != (exceptReservationId ?? 0))
            .Select(r => new { UnitId = r.StorageUnitId!.Value, r.MoveInDate, r.RentalMonths })
            .ToListAsync(ct);
        foreach (var b in bookings.Where(b => EndOf(b.MoveInDate, b.RentalMonths) >= from))
            busy.Add(b.UnitId);

        // A unit offered to another booking instead of its held one is taken until that customer answers.
        var proposals = await uow.Repository<Reservation>().Query()
            .Where(r => r.ProposedStorageUnitId != null && unitIds.Contains(r.ProposedStorageUnitId.Value)
                && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed)
                && r.MoveInDate <= to && r.ReservationId != (exceptReservationId ?? 0))
            .Select(r => new { UnitId = r.ProposedStorageUnitId!.Value, r.MoveInDate, r.RentalMonths })
            .ToListAsync(ct);
        foreach (var p in proposals.Where(p => EndOf(p.MoveInDate, p.RentalMonths) >= from))
            busy.Add(p.UnitId);

        // A unit offered to a renewing renter is held until they answer.
        var offered = await uow.Repository<Renewal>().Query()
            .Where(r => r.TransferStorageUnitId != null && unitIds.Contains(r.TransferStorageUnitId.Value)
                && r.Status == Features.Renewals.RenewalStatus.AwaitingCustomer && r.NewEndDate >= from
                && r.ContractId != (exceptContractId ?? 0))
            .Select(r => r.TransferStorageUnitId!.Value)
            .ToListAsync(ct);
        busy.UnionWith(offered);
        return busy;
    }
}
