namespace StorEase.Application.Features.StorageUnits.Dtos;

/// <summary>CurrentPrice is optional: the rent always comes from the pricing policy, so it defaults to the policy price.</summary>
public sealed record CreateStorageUnitRequest(
    int FacilityId, int FloorId, int UnitTypeId, string UnitCode, string? Zone, decimal? CurrentPrice, string? Note);

/// <summary>The facility cannot change. Floor and unit type can only change while the unit is Available.
/// Without CurrentPrice the price is kept, or re-read from the pricing policy when the unit type changes.</summary>
public sealed record UpdateStorageUnitRequest(
    int FloorId, int UnitTypeId, string UnitCode, string? Zone, decimal? CurrentPrice, string? Note);
