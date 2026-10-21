using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.AccessCodes.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;

namespace StorEase.Application.Features.AccessCodes;

public class AccessLogService(IUnitOfWork uow, ICurrentUser currentUser) : IAccessLogService
{
    public Task<Result<PagedResult<AccessLogDto>>> SearchAsync(
        int? storageUnitId, int? customerId, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct)
    {
        var query = Visible().AsNoTracking();
        if (storageUnitId is not null)
            query = query.Where(l => l.StorageUnitId == storageUnitId);
        if (customerId is not null)
            query = query.Where(l => l.AccessCode.Contract.CustomerId == customerId);
        return PageAsync(query, from, to, page, pageSize, ct);
    }

    private IQueryable<AccessLog> Visible()
    {
        var query = uow.Repository<AccessLog>().Query();
        if (StaffScope.SeesAllFacilities(currentUser.Role))
            return query;
        var facilityIds = StaffScope.FacilityIdsOf(uow, currentUser.UserId);
        return query.Where(l => facilityIds.Contains(l.StorageUnit.FacilityId));
    }

    public Task<Result<PagedResult<AccessLogDto>>> ListMineAsync(
        DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        var query = uow.Repository<AccessLog>().Query().AsNoTracking()
            .Where(l => l.AccessCode.Contract.CustomerId == userId);
        return PageAsync(query, from, to, page, pageSize, ct);
    }

    private static async Task<Result<PagedResult<AccessLogDto>>> PageAsync(
        IQueryable<AccessLog> query, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct)
    {
        if (from is not null && to is not null && from > to)
            return Error.Validation("'from' must not be after 'to'.");
        if (from is not null)
        {
            var start = from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(l => l.AccessTime >= start);
        }
        if (to is not null)
        {
            var end = to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(l => l.AccessTime < end);
        }

        return await query.OrderByDescending(l => l.AccessTime).ThenByDescending(l => l.AccessLogId)
            .Select(l => new AccessLogDto(
                l.AccessLogId, l.AccessCodeId, l.AccessCode.ContractId, l.AccessCode.Contract.CustomerId,
                l.AccessCode.Contract.Customer.FullName, l.StorageUnitId, l.StorageUnit.UnitCode,
                l.AccessTime, l.Result.ToString(), l.DeviceId))
            .ToPagedResultAsync(page, pageSize, ct);
    }
}
