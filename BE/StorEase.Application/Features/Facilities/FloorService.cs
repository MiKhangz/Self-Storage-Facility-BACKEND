using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Facilities.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Facilities;

public interface IFloorService
{
    Task<Result<IReadOnlyList<FacilityFloorDto>>> ListAsync(int facilityId, CancellationToken ct);
    Task<Result<FacilityFloorDto>> CreateAsync(int facilityId, FloorRequest request, CancellationToken ct);
    Task<Result<FacilityFloorDto>> UpdateAsync(int floorId, FloorRequest request, CancellationToken ct);
    Task<Result<bool>> DeleteAsync(int floorId, CancellationToken ct);
}

/// <summary>Floors are changed by an administrator, or by the facility's own manager.</summary>
public class FloorService(IUnitOfWork uow, ICurrentUser currentUser) : IFloorService
{
    private static readonly StorageUnitStatus[] Rented = [StorageUnitStatus.Reserved, StorageUnitStatus.Occupied];

    public async Task<Result<IReadOnlyList<FacilityFloorDto>>> ListAsync(int facilityId, CancellationToken ct)
    {
        if (!await uow.Repository<Facility>().Query().AnyAsync(f => f.FacilityId == facilityId, ct))
            return Error.NotFound("Facility not found.");
        return await Project(uow.Repository<Floor>().Query()
                .Where(f => f.FacilityId == facilityId && f.Status != FloorStatus.Removed).OrderBy(f => f.FloorNumber))
            .ToListAsync(ct);
    }

    public async Task<Result<FacilityFloorDto>> CreateAsync(int facilityId, FloorRequest request, CancellationToken ct)
    {
        if (!await uow.Repository<Facility>().Query().AnyAsync(f => f.FacilityId == facilityId, ct))
            return Error.NotFound("Facility not found.");
        if (!await StaffScope.CanManageAsync(uow, currentUser, facilityId, ct))
            return Error.Forbidden("You do not manage this facility.");
        if (await NumberTakenAsync(facilityId, request.FloorNumber, null, ct))
            return Error.Conflict($"Floor {request.FloorNumber} already exists in this facility.");

        var floor = new Floor
        {
            FacilityId = facilityId, FloorNumber = request.FloorNumber, Name = Clean(request.Name),
            Status = request.Status ?? FloorStatus.Open,
        };
        uow.Repository<Floor>().Add(floor);
        await uow.SaveChangesAsync(ct);
        return await GetAsync(floor.FloorId, ct);
    }

    /// <summary>
    /// Closing a floor (under construction, renovation) takes its free units off the market as Maintenance; rented
    /// ones keep their customers. Reopening puts them back on sale, except a unit still under a maintenance order.
    /// </summary>
    public async Task<Result<FacilityFloorDto>> UpdateAsync(int floorId, FloorRequest request, CancellationToken ct)
    {
        var floor = await uow.Repository<Floor>().FindAsync(floorId, ct);
        if (floor is null || floor.Status == FloorStatus.Removed)
            return Error.NotFound("Floor not found.");
        if (!await StaffScope.CanManageAsync(uow, currentUser, floor.FacilityId, ct))
            return Error.Forbidden("You do not manage this facility.");
        if (await NumberTakenAsync(floor.FacilityId, request.FloorNumber, floorId, ct))
            return Error.Conflict($"Floor {request.FloorNumber} already exists in this facility.");

        var status = request.Status ?? floor.Status;
        var wasOpen = floor.Status == FloorStatus.Open;
        if (wasOpen != (status == FloorStatus.Open))
        {
            var units = await uow.Repository<StorageUnit>().Query().Where(u => u.FloorId == floorId).ToListAsync(ct);
            if (wasOpen)
            {
                foreach (var unit in units.Where(u => u.Status == StorageUnitStatus.Available))
                    unit.Status = StorageUnitStatus.Maintenance;
            }
            else
            {
                var underRepair = await uow.Repository<MaintenanceOrder>().Query()
                    .Where(o => o.StorageUnit.FloorId == floorId && o.Status == MaintenanceOrderStatus.InProgress)
                    .Select(o => o.StorageUnitId).ToListAsync(ct);
                // shortcut: a unit held in Maintenance for return damage without an order is released too; staff
                // set it back from the unit board. Track who took a unit off the market if that becomes common.
                foreach (var unit in units.Where(u => u.Status == StorageUnitStatus.Maintenance && !underRepair.Contains(u.StorageUnitId)))
                    unit.Status = StorageUnitStatus.Available;
            }
        }
        floor.FloorNumber = request.FloorNumber;
        floor.Name = Clean(request.Name);
        floor.Status = status;
        await uow.SaveChangesAsync(ct);
        return await GetAsync(floorId, ct);
    }

    /// <summary>
    /// Refused while any unit on the floor is reserved or rented. Units that never had a booking, contract, ticket,
    /// repair or gate entry are deleted with the floor; otherwise (rule 10) they are retired and the floor is kept
    /// as Removed, out of every list.
    /// </summary>
    public async Task<Result<bool>> DeleteAsync(int floorId, CancellationToken ct)
    {
        var floor = await uow.Repository<Floor>().FindAsync(floorId, ct);
        if (floor is null || floor.Status == FloorStatus.Removed)
            return Error.NotFound("Floor not found.");
        if (!await StaffScope.CanManageAsync(uow, currentUser, floor.FacilityId, ct))
            return Error.Forbidden("You do not manage this facility.");

        var units = await uow.Repository<StorageUnit>().Query().Where(u => u.FloorId == floorId).ToListAsync(ct);
        var rented = string.Join(", ", units.Where(u => Rented.Contains(u.Status)).Select(u => u.UnitCode));
        if (rented.Length > 0)
            return Error.Conflict($"Customers still hold units on this floor: {rented}.");

        if (await HasHistoryAsync(units.Select(u => u.StorageUnitId).ToList(), ct))
        {
            foreach (var unit in units)
                unit.Status = StorageUnitStatus.Retired;
            floor.Status = FloorStatus.Removed;
        }
        else
        {
            foreach (var unit in units)
                uow.Repository<StorageUnit>().Remove(unit);
            uow.Repository<Floor>().Remove(floor);
        }
        await uow.SaveChangesAsync(ct);
        return true;
    }

    private async Task<bool> HasHistoryAsync(List<int> ids, CancellationToken ct) =>
        ids.Count > 0 && (
            await uow.Repository<Reservation>().Query().AnyAsync(x => x.StorageUnitId != null && ids.Contains(x.StorageUnitId.Value), ct) ||
            await uow.Repository<Contract>().Query().AnyAsync(x => ids.Contains(x.StorageUnitId), ct) ||
            await uow.Repository<MaintenanceOrder>().Query().AnyAsync(x => ids.Contains(x.StorageUnitId), ct) ||
            await uow.Repository<SupportTicket>().Query().AnyAsync(x => x.StorageUnitId != null && ids.Contains(x.StorageUnitId.Value), ct) ||
            await uow.Repository<AccessLog>().Query().AnyAsync(x => ids.Contains(x.StorageUnitId), ct));

    private async Task<FacilityFloorDto> GetAsync(int floorId, CancellationToken ct) =>
        await Project(uow.Repository<Floor>().Query().Where(f => f.FloorId == floorId)).FirstAsync(ct);

    private IQueryable<FacilityFloorDto> Project(IQueryable<Floor> floors)
    {
        var units = uow.Repository<StorageUnit>().Query();
        return floors.Select(f => new FacilityFloorDto(f.FloorId, f.FacilityId, f.FloorNumber, f.Name,
            units.Count(u => u.FloorId == f.FloorId && u.Status != StorageUnitStatus.Retired), f.Status,
            units.Count(u => u.FloorId == f.FloorId && Rented.Contains(u.Status))));
    }

    // A removed floor keeps its row for history but gives its number back.
    private Task<bool> NumberTakenAsync(int facilityId, int number, int? exceptFloorId, CancellationToken ct) =>
        uow.Repository<Floor>().Query().AnyAsync(f => f.FacilityId == facilityId && f.FloorNumber == number
            && f.FloorId != exceptFloorId && f.Status != FloorStatus.Removed, ct);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
