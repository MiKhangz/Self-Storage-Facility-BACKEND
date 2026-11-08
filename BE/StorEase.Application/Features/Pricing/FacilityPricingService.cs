using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Pricing.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Pricing;

/// <summary>
/// Each facility's own price list, in versions: one Draft at a time, published to Active, the old one Superseded.
/// A version prices some sizes; the rest, and every rental rule, come from the company standard policy.
/// Business Operations and Admin edit; a Facility Manager reads the lists of the facilities they manage.
/// </summary>
public interface IFacilityPricingService
{
    Task<Result<FacilityPriceListDto>> GetAsync(int facilityId, CancellationToken ct);
    /// <summary>A Draft copying the live version's prices (or empty); 409 while a Draft exists.</summary>
    Task<Result<FacilityPriceVersionDto>> CreateDraftAsync(int facilityId, CancellationToken ct);
    Task<Result<FacilityPriceVersionDto>> SetPriceAsync(int facilityId, int versionId, int unitTypeId, UpsertUnitPriceRequest request, CancellationToken ct);
    /// <summary>The size goes back to the company standard price in this Draft.</summary>
    Task<Result<FacilityPriceVersionDto>> RemovePriceAsync(int facilityId, int versionId, int unitTypeId, CancellationToken ct);
    /// <summary>The Draft goes live today; the version it replaces ends yesterday. Bookings keep what they were quoted.</summary>
    Task<Result<FacilityPriceVersionDto>> PublishAsync(int facilityId, int versionId, CancellationToken ct);
    Task<Result<bool>> DeleteDraftAsync(int facilityId, int versionId, CancellationToken ct);
}

public sealed class FacilityPricingService(IUnitOfWork uow, ICurrentUser currentUser) : IFacilityPricingService
{
    public async Task<Result<FacilityPriceListDto>> GetAsync(int facilityId, CancellationToken ct)
    {
        var facility = await uow.Repository<Facility>().Query().Where(f => f.FacilityId == facilityId)
            .Select(f => new { f.FacilityId, f.Name }).FirstOrDefaultAsync(ct);
        if (facility is null || !await CanReadAsync(facilityId, ct))
            return Error.NotFound("Facility not found.");

        var policy = await RentCalculator.PolicyForAsync(uow, VietnamTime.Today, ct);
        // Sorted in memory: SQLite (tests) cannot order by a decimal.
        var standard = policy is null ? [] : (await uow.Repository<UnitPrice>().Query().AsNoTracking().Include(p => p.UnitType)
            .Where(p => p.PricingPolicyId == policy.PricingPolicyId).ToListAsync(ct))
            .OrderBy(p => p.UnitType.AreaM2)
            .Select(p => new FacilitySizePriceDto(p.UnitTypeId, p.UnitType.Code, p.UnitType.Name, p.BasePricePerMonth, p.Discount3Month, p.Discount6Month, p.Discount12Month))
            .ToList();
        var versions = await Versions(facilityId).OrderByDescending(v => v.Version).ToListAsync(ct);
        return new FacilityPriceListDto(facility.FacilityId, facility.Name, policy?.Version, standard, versions.Select(ToDto).ToList());
    }

    public async Task<Result<FacilityPriceVersionDto>> CreateDraftAsync(int facilityId, CancellationToken ct)
    {
        if (!await uow.Repository<Facility>().Query().AnyAsync(f => f.FacilityId == facilityId, ct))
            return Error.NotFound("Facility not found.");
        if (await uow.Repository<FacilityPriceVersion>().Query().AnyAsync(v => v.FacilityId == facilityId && v.Status == PricingPolicyStatus.Draft, ct))
            return Error.Conflict("Publish or delete this facility's current draft first.");

        var live = await Versions(facilityId).Where(v => v.Status == PricingPolicyStatus.Active).OrderByDescending(v => v.Version).FirstOrDefaultAsync(ct);
        var number = (await uow.Repository<FacilityPriceVersion>().Query().Where(v => v.FacilityId == facilityId).MaxAsync(v => (int?)v.Version, ct) ?? 0) + 1;
        var draft = new FacilityPriceVersion
        {
            FacilityId = facilityId, Version = number, EffectiveFrom = VietnamTime.Today, Status = PricingPolicyStatus.Draft,
            CreatedBy = currentUser.UserId, CreatedAt = DateTime.UtcNow,
        };
        foreach (var p in live?.Prices ?? [])
            draft.Prices.Add(new FacilityUnitPrice
            {
                UnitTypeId = p.UnitTypeId, BasePricePerMonth = p.BasePricePerMonth,
                Discount3Month = p.Discount3Month, Discount6Month = p.Discount6Month, Discount12Month = p.Discount12Month,
            });
        uow.Repository<FacilityPriceVersion>().Add(draft);
        await uow.SaveChangesAsync(ct);
        return await VersionAsync(draft.FacilityPriceVersionId, ct);
    }

    public async Task<Result<FacilityPriceVersionDto>> SetPriceAsync(int facilityId, int versionId, int unitTypeId, UpsertUnitPriceRequest request, CancellationToken ct)
    {
        var draft = await DraftAsync(facilityId, versionId, ct);
        if (!draft.IsSuccess)
            return draft.Error;
        if (!await uow.Repository<UnitType>().Query().AnyAsync(t => t.UnitTypeId == unitTypeId, ct))
            return Error.NotFound("Unit type not found.");
        var price = draft.Value.Prices.FirstOrDefault(p => p.UnitTypeId == unitTypeId);
        if (price is null)
            draft.Value.Prices.Add(price = new FacilityUnitPrice { UnitTypeId = unitTypeId });
        price.BasePricePerMonth = Money.Round(request.BasePricePerMonth);
        price.Discount3Month = request.Discount3Month;
        price.Discount6Month = request.Discount6Month;
        price.Discount12Month = request.Discount12Month;
        await uow.SaveChangesAsync(ct);
        return await VersionAsync(versionId, ct);
    }

    public async Task<Result<FacilityPriceVersionDto>> RemovePriceAsync(int facilityId, int versionId, int unitTypeId, CancellationToken ct)
    {
        var draft = await DraftAsync(facilityId, versionId, ct);
        if (!draft.IsSuccess)
            return draft.Error;
        var price = draft.Value.Prices.FirstOrDefault(p => p.UnitTypeId == unitTypeId);
        if (price is null)
            return Error.NotFound("This facility has no own price for that size.");
        uow.Repository<FacilityUnitPrice>().Remove(price);
        await uow.SaveChangesAsync(ct);
        return await VersionAsync(versionId, ct);
    }

    public async Task<Result<FacilityPriceVersionDto>> PublishAsync(int facilityId, int versionId, CancellationToken ct)
    {
        var published = await uow.InTransactionAsync<int>(async () =>
        {
            var draft = await DraftAsync(facilityId, versionId, ct);
            if (!draft.IsSuccess)
                return draft.Error!;
            var today = VietnamTime.Today;
            var live = await uow.Repository<FacilityPriceVersion>().Query()
                .Where(v => v.FacilityId == facilityId && v.Status == PricingPolicyStatus.Active).ToListAsync(ct);
            foreach (var old in live)
            {
                old.Status = PricingPolicyStatus.Superseded;
                old.EffectiveTo = today.AddDays(-1);
            }
            draft.Value.Status = PricingPolicyStatus.Active;
            draft.Value.EffectiveFrom = today;
            return versionId;
        }, ct);
        return published.IsSuccess ? await VersionAsync(versionId, ct) : published.Error;
    }

    public async Task<Result<bool>> DeleteDraftAsync(int facilityId, int versionId, CancellationToken ct)
    {
        var draft = await DraftAsync(facilityId, versionId, ct);
        if (!draft.IsSuccess)
            return draft.Error;
        uow.Repository<FacilityPriceVersion>().Remove(draft.Value);
        await uow.SaveChangesAsync(ct);
        return true;
    }

    private async Task<Result<FacilityPriceVersion>> DraftAsync(int facilityId, int versionId, CancellationToken ct)
    {
        var version = await uow.Repository<FacilityPriceVersion>().Query().Include(v => v.Prices)
            .FirstOrDefaultAsync(v => v.FacilityPriceVersionId == versionId && v.FacilityId == facilityId, ct);
        if (version is null)
            return Error.NotFound("Price version not found.");
        // Published prices were quoted to customers, so they change only through a new version.
        if (version.Status != PricingPolicyStatus.Draft)
            return Error.Conflict("Only a Draft pricing policy can be edited.");
        return version;
    }

    private async Task<bool> CanReadAsync(int facilityId, CancellationToken ct) =>
        StaffScope.SeesAllFacilities(currentUser.Role)
        || await StaffScope.FacilityIdsOf(uow, currentUser.UserId).AnyAsync(id => id == facilityId, ct);

    private IQueryable<FacilityPriceVersion> Versions(int facilityId) => uow.Repository<FacilityPriceVersion>().Query().AsNoTracking()
        .Include(v => v.Prices).ThenInclude(p => p.UnitType)
        .Where(v => v.FacilityId == facilityId);

    private async Task<Result<FacilityPriceVersionDto>> VersionAsync(int versionId, CancellationToken ct) =>
        ToDto(await uow.Repository<FacilityPriceVersion>().Query().AsNoTracking()
            .Include(v => v.Prices).ThenInclude(p => p.UnitType).SingleAsync(v => v.FacilityPriceVersionId == versionId, ct));

    private static FacilityPriceVersionDto ToDto(FacilityPriceVersion v) => new(
        v.FacilityPriceVersionId, v.FacilityId, v.Version, v.EffectiveFrom, v.EffectiveTo, v.Status,
        v.Prices.OrderBy(p => p.UnitType.AreaM2).Select(p => new FacilitySizePriceDto(p.UnitTypeId, p.UnitType.Code, p.UnitType.Name,
            p.BasePricePerMonth, p.Discount3Month, p.Discount6Month, p.Discount12Month)).ToList());
}
