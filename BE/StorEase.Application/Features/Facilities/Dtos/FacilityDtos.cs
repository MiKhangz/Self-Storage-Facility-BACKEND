using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Facilities.Dtos;

public sealed record FacilityFloorDto(int FloorId, int FacilityId, int FloorNumber, string? Name, int UnitCount);

public sealed record FacilityUnitTypeDto(
    int UnitTypeId, string Code, string Name, decimal AreaM2, bool IsClimateControlled, int TotalUnits, int AvailableUnits, decimal? FromPrice);

public sealed record FacilityDetailDto(
    int FacilityId,
    string Code,
    string Name,
    string Address,
    string District,
    string? Phone,
    string? OpeningHours,
    FacilityStatus Status,
    int? ManagerId,
    string? ManagerName,
    int TotalUnits,
    int AvailableUnits,
    IReadOnlyList<FacilityFloorDto> Floors,
    IReadOnlyList<FacilityUnitTypeDto> UnitTypes);

/// <summary>Create and update share one shape; <see cref="ManagerId"/> must be a Facility Manager.</summary>
public sealed record FacilityRequest(
    string Code, string Name, string Address, string District, string? Phone, string? OpeningHours, int? ManagerId);

public sealed record FloorRequest(int FloorNumber, string? Name);

public sealed record UnitBoardUnitDto(
    int StorageUnitId, string UnitCode, string? Zone, StorageUnitStatus Status, int UnitTypeId, string UnitTypeName,
    decimal CurrentPrice, string? ReservationCode, int? ContractId, string? CustomerName);

public sealed record UnitBoardFloorDto(int FloorId, int FloorNumber, string? Name, IReadOnlyList<UnitBoardUnitDto> Units);

/// <summary>The colour-coded grid: every unit of a facility by floor, with counts per status.</summary>
public sealed record UnitBoardDto(
    int FacilityId, string FacilityName, IReadOnlyDictionary<StorageUnitStatus, int> StatusCounts, decimal OccupancyRate,
    IReadOnlyList<UnitBoardFloorDto> Floors);
