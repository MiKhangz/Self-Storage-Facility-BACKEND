using StorEase.Application.Common;
using StorEase.Application.Features.AccessCodes.Dtos;

namespace StorEase.Application.Features.AccessCodes;

public interface IAccessLogService
{
    /// <summary>Manager's search by unit, customer and date range (inclusive, UTC dates). Newest first.</summary>
    Task<Result<PagedResult<AccessLogDto>>> SearchAsync(
        int? storageUnitId, int? customerId, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct);

    /// <summary>The caller's own entries and exits. Newest first.</summary>
    Task<Result<PagedResult<AccessLogDto>>> ListMineAsync(DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct);
}
