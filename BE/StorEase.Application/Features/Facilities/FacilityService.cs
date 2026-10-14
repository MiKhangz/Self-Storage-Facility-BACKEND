using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Facilities.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Facilities;

public interface IFacilityService
{
    /// <summary>Active facilities, searchable by name/address/code and filterable by district. Admins may include inactive ones.</summary>
    Task<Result<PagedResult<FacilityListItemDto>>> ListAsync(
        string? search, string? district, bool includeInactive, int page, int pageSize, CancellationToken ct);
    Task<Result<FacilityDetailDto>> GetAsync(int facilityId, CancellationToken ct);
    Task<Result<FacilityDetailDto>> CreateAsync(FacilityRequest request, CancellationToken ct);
    Task<Result<FacilityDetailDto>> UpdateAsync(int facilityId, FacilityRequest request, CancellationToken ct);
    Task<Result<FacilityDetailDto>> SetActiveAsync(int facilityId, bool active, CancellationToken ct);
    Task<Result<UnitBoardDto>> GetUnitBoardAsync(int facilityId, CancellationToken ct);
}

public class FacilityService(IUnitOfWork uow, ICurrentUser currentUser) : IFacilityService
{
    public async Task<Result<PagedResult<FacilityListItemDto>>> ListAsync(
        string? search, string? district, bool includeInactive, int page, int pageSize, CancellationToken ct)
    {
        var query = uow.Repository<Facility>().Query();
        if (!(includeInactive && currentUser.Role == Roles.SystemAdministrator))
            query = query.Where(f => f.Status == FacilityStatus.Active);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(f => f.Name.Contains(term) || f.Address.Contains(term) || f.Code.Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(district))
            query = query.Where(f => f.District == district.Trim());

        return await query
            .OrderBy(f => f.Name)
            .Select(f => new FacilityListItemDto(
                f.FacilityId, f.Code, f.Name, f.Address, f.District, f.Phone, f.OpeningHours,
                f.StorageUnits.Count(u => u.Status == StorageUnitStatus.Available)))
            .ToPagedResultAsync(page, pageSize, ct);
    }

    public async Task<Result<FacilityDetailDto>> GetAsync(int facilityId, CancellationToken ct)
    {
        var facility = await uow.Repository<Facility>().Query().AsNoTracking()
            .Include(f => f.Manager)
            .Include(f => f.Floors)
            .Include(f => f.StorageUnits).ThenInclude(u => u.UnitType)
            .FirstOrDefaultAsync(f => f.FacilityId == facilityId, ct);
        // An inactive facility is only visible to staff, so customers cannot book it from a stale link.
        if (facility is null || (facility.Status != FacilityStatus.Active && currentUser.Role is "" or Roles.StorageCustomer))
            return Error.NotFound("Facility not found.");

        var prices = await ActivePricesAsync(ct);
        var units = facility.StorageUnits.Where(u => u.Status != StorageUnitStatus.Retired).ToList();
        return new FacilityDetailDto(
            facility.FacilityId, facility.Code, facility.Name, facility.Address, facility.District, facility.Phone,
            facility.OpeningHours, facility.Status, facility.ManagerId, facility.Manager?.FullName,
            units.Count, units.Count(u => u.Status == StorageUnitStatus.Available),
            facility.Floors.OrderBy(f => f.FloorNumber)
                .Select(f => new FacilityFloorDto(f.FloorId, f.FacilityId, f.FloorNumber, f.Name, units.Count(u => u.FloorId == f.FloorId)))
                .ToList(),
            units.GroupBy(u => u.UnitType).OrderBy(g => g.Key.AreaM2)
                .Select(g => new FacilityUnitTypeDto(g.Key.UnitTypeId, g.Key.Code, g.Key.Name, g.Key.AreaM2, g.Key.IsClimateControlled,
                    g.Count(), g.Count(u => u.Status == StorageUnitStatus.Available),
                    prices.TryGetValue(g.Key.UnitTypeId, out var price) ? price : null))
                .ToList());
    }

    public async Task<Result<FacilityDetailDto>> CreateAsync(FacilityRequest request, CancellationToken ct)
    {
        var problem = await CheckRequestAsync(request, null, ct);
        if (problem is not null)
            return problem;

        var facility = new Facility { Status = FacilityStatus.Active };
        Apply(facility, request);
        uow.Repository<Facility>().Add(facility);
        await uow.SaveChangesAsync(ct);
        return await GetAsync(facility.FacilityId, ct);
    }

    public async Task<Result<FacilityDetailDto>> UpdateAsync(int facilityId, FacilityRequest request, CancellationToken ct)
    {
        var facility = await uow.Repository<Facility>().FindAsync(facilityId, ct);
        if (facility is null)
            return Error.NotFound("Facility not found.");
        var problem = await CheckRequestAsync(request, facilityId, ct);
        if (problem is not null)
            return problem;

        Apply(facility, request);
        await uow.SaveChangesAsync(ct);
        return await GetAsync(facilityId, ct);
    }

    // Business rule 10: a facility with history is deactivated, never deleted.
    public async Task<Result<FacilityDetailDto>> SetActiveAsync(int facilityId, bool active, CancellationToken ct)
    {
        var facility = await uow.Repository<Facility>().FindAsync(facilityId, ct);
        if (facility is null)
            return Error.NotFound("Facility not found.");

        if (!active)
        {
            var inUse = await uow.Repository<StorageUnit>().Query().AnyAsync(u => u.FacilityId == facilityId
                && (u.Status == StorageUnitStatus.Reserved || u.Status == StorageUnitStatus.Occupied), ct);
            var waiting = await uow.Repository<Reservation>().Query().AnyAsync(r => r.FacilityId == facilityId
                && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed), ct);
            if (inUse || waiting)
                return Error.Conflict("The facility still has reserved or occupied units, or open reservations.");
        }

        facility.Status = active ? FacilityStatus.Active : FacilityStatus.Inactive;
        await uow.SaveChangesAsync(ct);
        return await GetAsync(facilityId, ct);
    }

    public async Task<Result<UnitBoardDto>> GetUnitBoardAsync(int facilityId, CancellationToken ct)
    {
        var facility = await uow.Repository<Facility>().Query().AsNoTracking()
            .Include(f => f.Floors)
            .FirstOrDefaultAsync(f => f.FacilityId == facilityId, ct);
        if (facility is null)
            return Error.NotFound("Facility not found.");
        if (!StaffScope.SeesAllFacilities(currentUser.Role)
            && !await StaffScope.FacilityIdsOf(uow, currentUser.UserId).AnyAsync(id => id == facilityId, ct))
            return Error.Forbidden("You are not assigned to this facility.");

        var reservations = uow.Repository<Reservation>().Query();
        var openContracts = uow.Repository<Contract>().Query()
            .Where(c => c.Status != ContractStatus.Closed && c.Status != ContractStatus.Terminated);
        var units = await uow.Repository<StorageUnit>().Query().AsNoTracking()
            .Where(u => u.FacilityId == facilityId && u.Status != StorageUnitStatus.Retired)
            .OrderBy(u => u.UnitCode)
            .Select(u => new
            {
                u.FloorId,
                Unit = new UnitBoardUnitDto(
                    u.StorageUnitId, u.UnitCode, u.Zone, u.Status, u.UnitTypeId, u.UnitType.Name, u.CurrentPrice,
                    reservations
                        .Where(r => r.StorageUnitId == u.StorageUnitId && r.Status == ReservationStatus.Confirmed)
                        .Select(r => r.ReservationCode).FirstOrDefault(),
                    openContracts.Where(c => c.StorageUnitId == u.StorageUnitId)
                        .OrderByDescending(c => c.ContractId).Select(c => (int?)c.ContractId).FirstOrDefault(),
                    openContracts.Where(c => c.StorageUnitId == u.StorageUnitId)
                        .OrderByDescending(c => c.ContractId).Select(c => c.Customer.FullName).FirstOrDefault()),
            })
            .ToListAsync(ct);

        var counts = Enum.GetValues<StorageUnitStatus>().Where(s => s != StorageUnitStatus.Retired)
            .ToDictionary(s => s, s => units.Count(u => u.Unit.Status == s));
        var occupied = counts[StorageUnitStatus.Occupied];
        var rate = units.Count == 0 ? 0m : Math.Round(occupied * 100m / units.Count, 1);
        return new UnitBoardDto(facility.FacilityId, facility.Name, counts, rate,
            facility.Floors.OrderBy(f => f.FloorNumber)
                .Select(f => new UnitBoardFloorDto(f.FloorId, f.FloorNumber, f.Name,
                    units.Where(u => u.FloorId == f.FloorId).Select(u => u.Unit).ToList()))
                .ToList());
    }

    private async Task<Error?> CheckRequestAsync(FacilityRequest request, int? facilityId, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await uow.Repository<Facility>().Query().AnyAsync(f => f.Code == code && f.FacilityId != facilityId, ct))
            return Error.Conflict($"A facility with code {code} already exists.");
        if (request.ManagerId is not null)
        {
            var isManager = await uow.Repository<User>().Query().AnyAsync(u => u.UserId == request.ManagerId
                && u.Role.RoleName == Roles.FacilityManager && u.Status == UserStatus.Active, ct);
            if (!isManager)
                return Error.Validation("The manager must be an active Facility Manager account.");
        }
        return null;
    }

    private static void Apply(Facility facility, FacilityRequest request)
    {
        facility.Code = request.Code.Trim().ToUpperInvariant();
        facility.Name = request.Name.Trim();
        facility.Address = request.Address.Trim();
        facility.District = request.District.Trim();
        facility.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        facility.OpeningHours = string.IsNullOrWhiteSpace(request.OpeningHours) ? null : request.OpeningHours.Trim();
        facility.ManagerId = request.ManagerId;
    }

    // "From" price per unit type in the active policy, before any long-stay discount.
    private async Task<Dictionary<int, decimal>> ActivePricesAsync(CancellationToken ct) =>
        await uow.Repository<UnitPrice>().Query()
            .Where(p => p.PricingPolicy.Status == PricingPolicyStatus.Active)
            .GroupBy(p => p.UnitTypeId)
            .Select(g => new { g.Key, Price = g.Min(p => p.BasePricePerMonth) })
            .ToDictionaryAsync(x => x.Key, x => x.Price, ct);
}
