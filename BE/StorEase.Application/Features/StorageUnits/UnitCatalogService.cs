using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Reservations.Validators;
using StorEase.Application.Features.StorageUnits.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.StorageUnits;

public interface IUnitCatalogService
{
    Task<Result<IReadOnlyList<UnitTypeDto>>> ListUnitTypesAsync(CancellationToken ct);
    Task<Result<UnitTypeDto>> GetUnitTypeAsync(int unitTypeId, CancellationToken ct);
    Task<Result<UnitTypeDto>> CreateUnitTypeAsync(UnitTypeRequest request, CancellationToken ct);
    Task<Result<UnitTypeDto>> UpdateUnitTypeAsync(int unitTypeId, UnitTypeRequest request, CancellationToken ct);
    Task<Result<bool>> DeleteUnitTypeAsync(int unitTypeId, CancellationToken ct);
    Task<Result<UnitAvailabilityDto>> GetAvailabilityAsync(int facilityId, int unitTypeId, DateOnly moveInDate, CancellationToken ct);
    Task<Result<UnitPriceQuoteDto>> QuoteAsync(int facilityId, int unitTypeId, DateOnly moveInDate, int rentalMonths, CancellationToken ct);
}

public sealed class UnitCatalogService(IUnitOfWork uow) : IUnitCatalogService
{
    public async Task<Result<IReadOnlyList<UnitTypeDto>>> ListUnitTypesAsync(CancellationToken ct)
    {
        var items = await uow.Repository<UnitType>().Query()
            .OrderBy(t => t.Code)
            .Select(ProjectUnitType())
            .ToListAsync(ct);
        return items;
    }

    public async Task<Result<UnitTypeDto>> GetUnitTypeAsync(int unitTypeId, CancellationToken ct)
    {
        var dto = await uow.Repository<UnitType>().Query()
            .Where(t => t.UnitTypeId == unitTypeId)
            .Select(ProjectUnitType())
            .FirstOrDefaultAsync(ct);
        return dto is null ? Error.NotFound("Unit type not found.") : dto;
    }

    public async Task<Result<UnitTypeDto>> CreateUnitTypeAsync(UnitTypeRequest request, CancellationToken ct)
    {
        var code = request.Code.Trim();
        if (await uow.Repository<UnitType>().Query().AnyAsync(t => t.Code == code, ct))
            return Error.Conflict("A unit type with this code already exists.");

        var entity = new UnitType
        {
            Code = code,
            Name = request.Name.Trim(),
            AreaM2 = request.AreaM2,
            Width = request.Width,
            Depth = request.Depth,
            Height = request.Height,
            IsClimateControlled = request.IsClimateControlled,
        };
        uow.Repository<UnitType>().Add(entity);
        await uow.SaveChangesAsync(ct);
        return await GetUnitTypeAsync(entity.UnitTypeId, ct);
    }

    public async Task<Result<UnitTypeDto>> UpdateUnitTypeAsync(int unitTypeId, UnitTypeRequest request, CancellationToken ct)
    {
        var entity = await uow.Repository<UnitType>().FindAsync(unitTypeId, ct);
        if (entity is null)
            return Error.NotFound("Unit type not found.");

        var code = request.Code.Trim();
        if (await uow.Repository<UnitType>().Query().AnyAsync(t => t.UnitTypeId != unitTypeId && t.Code == code, ct))
            return Error.Conflict("A unit type with this code already exists.");

        entity.Code = code;
        entity.Name = request.Name.Trim();
        entity.AreaM2 = request.AreaM2;
        entity.Width = request.Width;
        entity.Depth = request.Depth;
        entity.Height = request.Height;
        entity.IsClimateControlled = request.IsClimateControlled;
        await uow.SaveChangesAsync(ct);
        return await GetUnitTypeAsync(entity.UnitTypeId, ct);
    }

    public async Task<Result<bool>> DeleteUnitTypeAsync(int unitTypeId, CancellationToken ct)
    {
        var entity = await uow.Repository<UnitType>().FindAsync(unitTypeId, ct);
        if (entity is null)
            return Error.NotFound("Unit type not found.");

        var isReferenced = await uow.Repository<StorageUnit>().Query().AnyAsync(u => u.UnitTypeId == unitTypeId, ct)
            || await uow.Repository<UnitPrice>().Query().AnyAsync(p => p.UnitTypeId == unitTypeId, ct);
        if (isReferenced)
            return Error.Conflict("A unit type that has units or pricing history cannot be deleted.");

        uow.Repository<UnitType>().Remove(entity);
        await uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task<Result<UnitAvailabilityDto>> GetAvailabilityAsync(
        int facilityId, int unitTypeId, DateOnly moveInDate, CancellationToken ct)
    {
        var facilityExists = await uow.Repository<Facility>().Query()
            .AnyAsync(f => f.FacilityId == facilityId && f.Status == FacilityStatus.Active, ct);
        if (!facilityExists)
            return Error.NotFound("Facility not found.");
        if (!await uow.Repository<UnitType>().Query().AnyAsync(t => t.UnitTypeId == unitTypeId, ct))
            return Error.NotFound("Unit type not found.");

        var units = uow.Repository<StorageUnit>().Query()
            .Where(u => u.FacilityId == facilityId && u.UnitTypeId == unitTypeId && u.Status != StorageUnitStatus.Retired);
        var total = await units.CountAsync(ct);
        var available = await units.CountAsync(u => u.Status == StorageUnitStatus.Available, ct);
        return new UnitAvailabilityDto(facilityId, unitTypeId, moveInDate, available, total);
    }

    public async Task<Result<UnitPriceQuoteDto>> QuoteAsync(
        int facilityId, int unitTypeId, DateOnly moveInDate, int rentalMonths, CancellationToken ct)
    {
        if (rentalMonths is < 1 or > CreateReservationRequestValidator.MaxRentalMonths)
            return Error.Validation($"Rental months must be between 1 and {CreateReservationRequestValidator.MaxRentalMonths}.");
        var availability = await GetAvailabilityAsync(facilityId, unitTypeId, moveInDate, ct);
        if (!availability.IsSuccess)
            return availability.Error!;
        if (availability.Value.AvailableUnits == 0)
            return Error.Conflict("No unit of this type is available at this facility.");

        var policy = await uow.Repository<PricingPolicy>().Query()
            .Where(p => p.Status == PricingPolicyStatus.Active && p.EffectiveFrom <= moveInDate
                && (p.EffectiveTo == null || p.EffectiveTo >= moveInDate))
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync(ct);
        if (policy is null)
            return Error.NotFound("No active pricing policy applies to the move-in date.");

        var price = await uow.Repository<UnitPrice>().Query()
            .Where(p => p.PricingPolicyId == policy.PricingPolicyId && p.UnitTypeId == unitTypeId)
            .Select(p => new { p.BasePricePerMonth, p.Discount3Month, p.Discount6Month, p.Discount12Month })
            .FirstOrDefaultAsync(ct);
        if (price is null)
            return Error.NotFound("No price is configured for this unit type in the active policy.");

        var discount = rentalMonths >= 12 ? price.Discount12Month
            : rentalMonths >= 6 ? price.Discount6Month
            : rentalMonths >= 3 ? price.Discount3Month : 0m;
        var monthlyRent = Money.Round(price.BasePricePerMonth * (1m - discount / 100m));
        var rentSubtotal = Money.Round(monthlyRent * rentalMonths);
        var deposit = Money.Round(monthlyRent * policy.DepositMonths);
        var fees = await uow.Repository<FeeType>().Query()
            .Where(f => f.PricingPolicyId == policy.PricingPolicyId)
            .Select(f => new { f.Code, f.Name, f.Amount, f.IsPercentage })
            .ToListAsync(ct);
        var quoteFees = fees.Select(f => new QuoteFeeDto(f.Code, f.Name,
            Money.Round(f.IsPercentage ? rentSubtotal * f.Amount / 100m : f.Amount))).ToList();
        var total = Money.Round(rentSubtotal + deposit + quoteFees.Sum(f => f.Amount));
        return new UnitPriceQuoteDto(facilityId, unitTypeId, moveInDate, rentalMonths,
            policy.PricingPolicyId, policy.Version, monthlyRent, rentSubtotal, deposit, quoteFees, total);
    }

    private static Expression<Func<UnitType, UnitTypeDto>> ProjectUnitType() =>
        t => new UnitTypeDto(t.UnitTypeId, t.Code, t.Name, t.AreaM2, t.Width, t.Depth, t.Height, t.IsClimateControlled);
}
