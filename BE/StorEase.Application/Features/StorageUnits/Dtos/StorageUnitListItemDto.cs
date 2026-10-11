using StorEase.Domain.Enums;

namespace StorEase.Application.Features.StorageUnits.Dtos;

public sealed record StorageUnitListItemDto(
    int StorageUnitId,
    string UnitCode,
    int FacilityId,
    int FloorId,
    string? FloorName,
    string? Zone,
    StorageUnitStatus Status,
    decimal CurrentPrice,
    int UnitTypeId,
    string UnitTypeCode,
    string UnitTypeName,
    decimal AreaM2);
