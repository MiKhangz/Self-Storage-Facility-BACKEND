using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Facilities.Dtos;

/// <summary><c>RentedUnits</c> are Reserved or Occupied: while any remain, the floor cannot be removed.</summary>
public sealed record FacilityFloorDto(int FloorId, int FacilityId, int FloorNumber, string? Name, int UnitCount, FloorStatus Status, int RentedUnits);

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

/// <summary><c>Status</c> left out keeps the floor's current one (a new floor is Open).</summary>
public sealed record FloorRequest(int FloorNumber, string? Name, FloorStatus? Status = null);

public sealed record UnitBoardUnitDto(
    int StorageUnitId, string UnitCode, string? Zone, StorageUnitStatus Status, int UnitTypeId, string UnitTypeName,
    decimal CurrentPrice, string? ReservationCode, int? ContractId, string? CustomerName);

public sealed record UnitBoardFloorDto(int FloorId, int FloorNumber, string? Name, IReadOnlyList<UnitBoardUnitDto> Units);

/// <summary>The colour-coded grid: every unit of a facility by floor, with counts per status.</summary>
public sealed record UnitBoardDto(
    int FacilityId, string FacilityName, IReadOnlyDictionary<StorageUnitStatus, int> StatusCounts, decimal OccupancyRate,
    IReadOnlyList<UnitBoardFloorDto> Floors);
