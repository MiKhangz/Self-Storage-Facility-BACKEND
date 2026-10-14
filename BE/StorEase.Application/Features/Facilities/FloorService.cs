using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Facilities.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;

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
    public async Task<Result<IReadOnlyList<FacilityFloorDto>>> ListAsync(int facilityId, CancellationToken ct)
    {
        if (!await uow.Repository<Facility>().Query().AnyAsync(f => f.FacilityId == facilityId, ct))
            return Error.NotFound("Facility not found.");
        var units = uow.Repository<StorageUnit>().Query();
        return await uow.Repository<Floor>().Query().Where(f => f.FacilityId == facilityId).OrderBy(f => f.FloorNumber)
            .Select(f => new FacilityFloorDto(f.FloorId, f.FacilityId, f.FloorNumber, f.Name, units.Count(u => u.FloorId == f.FloorId)))
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

        var floor = new Floor { FacilityId = facilityId, FloorNumber = request.FloorNumber, Name = Clean(request.Name) };
        uow.Repository<Floor>().Add(floor);
        await uow.SaveChangesAsync(ct);
        return new FacilityFloorDto(floor.FloorId, facilityId, floor.FloorNumber, floor.Name, 0);
    }

    public async Task<Result<FacilityFloorDto>> UpdateAsync(int floorId, FloorRequest request, CancellationToken ct)
    {
        var floor = await uow.Repository<Floor>().FindAsync(floorId, ct);
        if (floor is null)
            return Error.NotFound("Floor not found.");
        if (!await StaffScope.CanManageAsync(uow, currentUser, floor.FacilityId, ct))
            return Error.Forbidden("You do not manage this facility.");
        if (await NumberTakenAsync(floor.FacilityId, request.FloorNumber, floorId, ct))
            return Error.Conflict($"Floor {request.FloorNumber} already exists in this facility.");

        floor.FloorNumber = request.FloorNumber;
        floor.Name = Clean(request.Name);
        await uow.SaveChangesAsync(ct);
        var unitCount = await uow.Repository<StorageUnit>().Query().CountAsync(u => u.FloorId == floorId, ct);
        return new FacilityFloorDto(floor.FloorId, floor.FacilityId, floor.FloorNumber, floor.Name, unitCount);
    }

    // A floor has no status column; deleting is allowed only while nothing has ever been placed on it.
    public async Task<Result<bool>> DeleteAsync(int floorId, CancellationToken ct)
    {
        var floor = await uow.Repository<Floor>().FindAsync(floorId, ct);
        if (floor is null)
            return Error.NotFound("Floor not found.");
        if (!await StaffScope.CanManageAsync(uow, currentUser, floor.FacilityId, ct))
            return Error.Forbidden("You do not manage this facility.");
        if (await uow.Repository<StorageUnit>().Query().AnyAsync(u => u.FloorId == floorId, ct))
            return Error.Conflict("The floor still has units; move or retire them first.");

        uow.Repository<Floor>().Remove(floor);
        await uow.SaveChangesAsync(ct);
        return true;
    }

    private Task<bool> NumberTakenAsync(int facilityId, int number, int? exceptFloorId, CancellationToken ct) =>
        uow.Repository<Floor>().Query().AnyAsync(f => f.FacilityId == facilityId && f.FloorNumber == number && f.FloorId != exceptFloorId, ct);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
