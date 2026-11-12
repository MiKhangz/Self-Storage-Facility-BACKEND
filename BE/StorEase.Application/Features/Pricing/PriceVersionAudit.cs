using Microsoft.EntityFrameworkCore;
using StorEase.Application.Features.Pricing.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Pricing;

/// <summary>
/// The history details of price versions: who drafted each, who published it (from the activity log, which records
/// every publish), and how many bookings were quoted on it.
/// </summary>
internal static class PriceVersionAudit
{
    /// <param name="publishAction">The activity log action of publishing, e.g. "POST FacilityPrices.Publish".</param>
    /// <param name="bookingVersionIds">The version each booking was quoted on, e.g. Reservation.FacilityPriceVersionId.</param>
    public static async Task<Dictionary<int, PriceVersionAuditDto>> LoadAsync(IUnitOfWork uow, string publishAction,
        IReadOnlyCollection<(int Id, int CreatedBy)> versions, IQueryable<int?> bookingVersionIds, CancellationToken ct)
    {
        var keys = versions.Select(v => v.Id.ToString()).ToList();
        var published = (await uow.Repository<ActivityLog>().Query().AsNoTracking()
                .Where(l => l.Action == publishAction && l.Result == ActivityResult.Success && keys.Contains(l.EntityId!))
                .Select(l => new { l.EntityId, l.UserId, l.CreatedAt }).ToListAsync(ct))
            .GroupBy(l => int.Parse(l.EntityId!)).ToDictionary(g => g.Key, g => g.MaxBy(l => l.CreatedAt)!);

        var userIds = versions.Select(v => v.CreatedBy).Concat(published.Values.Select(p => p.UserId)).Distinct().ToList();
        var names = await uow.Repository<User>().Query().AsNoTracking().Where(u => userIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.FullName, ct);

        var ids = versions.Select(v => (int?)v.Id).ToList();
        var bookings = await bookingVersionIds.Where(id => ids.Contains(id)).GroupBy(id => id!.Value)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);

        return versions.ToDictionary(v => v.Id, v =>
        {
            var p = published.GetValueOrDefault(v.Id);
            return new PriceVersionAuditDto(names.GetValueOrDefault(v.CreatedBy),
                p is null ? null : names.GetValueOrDefault(p.UserId), p?.CreatedAt, bookings.GetValueOrDefault(v.Id));
        });
    }
}
