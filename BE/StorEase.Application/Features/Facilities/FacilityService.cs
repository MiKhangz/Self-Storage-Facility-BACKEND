using StorEase.Application.Common;
using StorEase.Application.Features.Facilities.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Facilities;

public interface IFacilityService
{
    Task<Result<PagedResult<FacilityListItemDto>>> ListAsync(int page, int pageSize, CancellationToken ct);
}

public class FacilityService(IUnitOfWork uow) : IFacilityService
{
    public async Task<Result<PagedResult<FacilityListItemDto>>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        return await uow.Repository<Facility>().Query()
            .Where(f => f.Status == FacilityStatus.Active)
            .OrderBy(f => f.Name)
            .Select(f => new FacilityListItemDto(
                f.FacilityId, f.Code, f.Name, f.Address, f.District, f.Phone, f.OpeningHours,
                f.StorageUnits.Count(u => u.Status == StorageUnitStatus.Available)))
            .ToPagedResultAsync(page, pageSize, ct);
    }
}
