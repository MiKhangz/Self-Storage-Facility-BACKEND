using StorEase.Application.Common;
using StorEase.Application.Features.StorageUnits.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.StorageUnits;

public interface IStorageUnitService
{
    Task<Result<PagedResult<StorageUnitListItemDto>>> ListAsync(
        int? facilityId, StorageUnitStatus? status, int page, int pageSize, CancellationToken ct);
}

public class StorageUnitService(IUnitOfWork uow) : IStorageUnitService
{
    public async Task<Result<PagedResult<StorageUnitListItemDto>>> ListAsync(
        int? facilityId, StorageUnitStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var query = uow.Repository<StorageUnit>().Query();
        if (facilityId is not null)
            query = query.Where(u => u.FacilityId == facilityId);
        if (status is not null)
            query = query.Where(u => u.Status == status);

        return await query
            .OrderBy(u => u.UnitCode)
            .Select(u => new StorageUnitListItemDto(
                u.StorageUnitId, u.UnitCode, u.FacilityId, u.FloorId, u.Floor.Name, u.Zone, u.Status,
                u.CurrentPrice, u.UnitTypeId, u.UnitType.Code, u.UnitType.Name, u.UnitType.AreaM2))
            .ToPagedResultAsync(page, pageSize, ct);
    }
}
