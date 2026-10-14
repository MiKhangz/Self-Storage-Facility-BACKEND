namespace StorEase.Application.Features.StorageUnits.Dtos;

public sealed record CreateStorageUnitRequest(
    int FacilityId, int FloorId, int UnitTypeId, string UnitCode, string? Zone, decimal CurrentPrice, string? Note);

/// <summary>The facility cannot change. Floor and unit type can only change while the unit is Available.</summary>
public sealed record UpdateStorageUnitRequest(
    int FloorId, int UnitTypeId, string UnitCode, string? Zone, decimal CurrentPrice, string? Note);
