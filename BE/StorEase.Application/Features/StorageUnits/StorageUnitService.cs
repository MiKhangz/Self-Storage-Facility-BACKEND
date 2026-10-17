using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Pricing;
using StorEase.Application.Features.StorageUnits.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.StorageUnits;

public interface IStorageUnitService
{
    Task<Result<PagedResult<StorageUnitListItemDto>>> ListAsync(
        int? facilityId, StorageUnitStatus? status, int? unitTypeId, string? search, int page, int pageSize, CancellationToken ct);
    Task<Result<StorageUnitListItemDto>> GetAsync(int storageUnitId, CancellationToken ct);
    Task<Result<StorageUnitListItemDto>> CreateAsync(CreateStorageUnitRequest request, CancellationToken ct);
    Task<Result<StorageUnitListItemDto>> UpdateAsync(int storageUnitId, UpdateStorageUnitRequest request, CancellationToken ct);

    /// <summary>Takes an Available unit out of service, or puts a Retired one back (business rule 10: never deleted).</summary>
    Task<Result<StorageUnitListItemDto>> SetRetiredAsync(int storageUnitId, bool retired, CancellationToken ct);
}

public class StorageUnitService(IUnitOfWork uow, ICurrentUser currentUser) : IStorageUnitService
{
    public async Task<Result<PagedResult<StorageUnitListItemDto>>> ListAsync(
        int? facilityId, StorageUnitStatus? status, int? unitTypeId, string? search, int page, int pageSize, CancellationToken ct)
    {
        var query = uow.Repository<StorageUnit>().Query();
        if (facilityId is not null)
            query = query.Where(u => u.FacilityId == facilityId);
        if (status is not null)
            query = query.Where(u => u.Status == status);
        if (unitTypeId is not null)
            query = query.Where(u => u.UnitTypeId == unitTypeId);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.UnitCode.Contains(search.Trim()));

        return await Project(query.OrderBy(u => u.UnitCode)).ToPagedResultAsync(page, pageSize, ct);
    }

    public async Task<Result<StorageUnitListItemDto>> GetAsync(int storageUnitId, CancellationToken ct)
    {
        var dto = await Project(uow.Repository<StorageUnit>().Query().Where(u => u.StorageUnitId == storageUnitId)).FirstOrDefaultAsync(ct);
        return dto is null ? Error.NotFound("Storage unit not found.") : dto;
    }

    public async Task<Result<StorageUnitListItemDto>> CreateAsync(CreateStorageUnitRequest request, CancellationToken ct)
    {
        if (!await uow.Repository<Facility>().Query().AnyAsync(f => f.FacilityId == request.FacilityId, ct))
            return Error.NotFound("Facility not found.");
        if (!await StaffScope.CanManageAsync(uow, currentUser, request.FacilityId, ct))
            return Error.Forbidden("You do not manage this facility.");
        var problem = await CheckPlacementAsync(request.FacilityId, request.FloorId, request.UnitTypeId, request.UnitCode, null, ct);
        if (problem is not null)
            return problem;

        var unit = new StorageUnit
        {
            FacilityId = request.FacilityId,
            FloorId = request.FloorId,
            UnitTypeId = request.UnitTypeId,
            UnitCode = request.UnitCode.Trim().ToUpperInvariant(),
            Zone = Clean(request.Zone),
            CurrentPrice = request.CurrentPrice is { } price ? Money.Round(price) : await PolicyPriceAsync(request.UnitTypeId, ct),
            Note = Clean(request.Note),
            Status = StorageUnitStatus.Available,
        };
        uow.Repository<StorageUnit>().Add(unit);
        await uow.SaveChangesAsync(ct);
        return await GetAsync(unit.StorageUnitId, ct);
    }

    public async Task<Result<StorageUnitListItemDto>> UpdateAsync(int storageUnitId, UpdateStorageUnitRequest request, CancellationToken ct)
    {
        var unit = await uow.Repository<StorageUnit>().FindAsync(storageUnitId, ct);
        if (unit is null)
            return Error.NotFound("Storage unit not found.");
        if (!await StaffScope.CanManageAsync(uow, currentUser, unit.FacilityId, ct))
            return Error.Forbidden("You do not manage this facility.");
        // A reservation or contract was made for this size and location; changing them would misdescribe what was rented.
        if ((unit.UnitTypeId != request.UnitTypeId || unit.FloorId != request.FloorId) && unit.Status != StorageUnitStatus.Available)
            return Error.Conflict($"Unit {unit.UnitCode} is {unit.Status}; its type and floor can only change while it is Available.");
        var problem = await CheckPlacementAsync(unit.FacilityId, request.FloorId, request.UnitTypeId, request.UnitCode, storageUnitId, ct);
        if (problem is not null)
            return problem;

        if (request.CurrentPrice is { } price)
            unit.CurrentPrice = Money.Round(price);
        else if (unit.UnitTypeId != request.UnitTypeId)
            unit.CurrentPrice = await PolicyPriceAsync(request.UnitTypeId, ct);
        unit.FloorId = request.FloorId;
        unit.UnitTypeId = request.UnitTypeId;
        unit.UnitCode = request.UnitCode.Trim().ToUpperInvariant();
        unit.Zone = Clean(request.Zone);
        unit.Note = Clean(request.Note);
        await uow.SaveChangesAsync(ct);
        return await GetAsync(storageUnitId, ct);
    }

    public async Task<Result<StorageUnitListItemDto>> SetRetiredAsync(int storageUnitId, bool retired, CancellationToken ct)
    {
        var unit = await uow.Repository<StorageUnit>().FindAsync(storageUnitId, ct);
        if (unit is null)
            return Error.NotFound("Storage unit not found.");
        if (!await StaffScope.CanManageAsync(uow, currentUser, unit.FacilityId, ct))
            return Error.Forbidden("You do not manage this facility.");

        var (from, to) = retired
            ? (StorageUnitStatus.Available, StorageUnitStatus.Retired)
            : (StorageUnitStatus.Retired, StorageUnitStatus.Available);
        if (unit.Status != from)
            return Error.Conflict($"Unit {unit.UnitCode} is {unit.Status}; only an {from} unit can become {to}.");

        unit.Status = to;
        await uow.SaveChangesAsync(ct);
        return await GetAsync(storageUnitId, ct);
    }

    private async Task<Error?> CheckPlacementAsync(
        int facilityId, int floorId, int unitTypeId, string unitCode, int? exceptUnitId, CancellationToken ct)
    {
        if (!await uow.Repository<Floor>().Query().AnyAsync(f => f.FloorId == floorId && f.FacilityId == facilityId, ct))
            return Error.Validation("The floor does not belong to this facility.");
        if (!await uow.Repository<UnitType>().Query().AnyAsync(t => t.UnitTypeId == unitTypeId, ct))
            return Error.NotFound("Unit type not found.");
        var code = unitCode.Trim().ToUpperInvariant();
        if (await uow.Repository<StorageUnit>().Query().AnyAsync(u => u.FacilityId == facilityId && u.UnitCode == code && u.StorageUnitId != exceptUnitId, ct))
            return Error.Conflict($"Unit code {code} is already used in this facility.");
        return null;
    }

    private static IQueryable<StorageUnitListItemDto> Project(IQueryable<StorageUnit> query) =>
        query.Select(u => new StorageUnitListItemDto(
            u.StorageUnitId, u.UnitCode, u.FacilityId, u.FloorId, u.Floor.Name, u.Zone, u.Status,
            u.CurrentPrice, u.UnitTypeId, u.UnitType.Code, u.UnitType.Name, u.UnitType.AreaM2, u.Note));

    /// <summary>
    /// The active policy's monthly base price for a unit type, or 0 when none is set. Display only: quotes, deposits and
    /// contracts read the policy through RentCalculator, never the unit's own price.
    /// </summary>
    private async Task<decimal> PolicyPriceAsync(int unitTypeId, CancellationToken ct)
    {
        var policy = await RentCalculator.PolicyForAsync(uow, VietnamTime.Today, ct);
        if (policy is null)
            return 0m;
        return await uow.Repository<UnitPrice>().Query()
            .Where(p => p.PricingPolicyId == policy.PricingPolicyId && p.UnitTypeId == unitTypeId)
            .Select(p => p.BasePricePerMonth).FirstOrDefaultAsync(ct);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
