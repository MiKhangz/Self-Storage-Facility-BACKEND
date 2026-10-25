using FluentValidation;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.CheckIns;

public sealed record CheckInRequest(int ContractId, string? Note);

public sealed record CheckInDto(
    int CheckInId, int ContractId, string ContractNumber, string CustomerName, int StorageUnitId, string UnitCode,
    int RecordedBy, string RecordedByName, DateTime CheckInAt, DateTime? CheckOutAt, string? Note,
    CheckOutMethod? CheckOutMethod, string? CheckOutReason);

/// <summary>Staff close a visit the customer left without checking out: when they left (Vietnam time) and why.</summary>
public sealed record MarkLeftRequest(DateTime LeftAt, string Reason);

public class MarkLeftRequestValidator : AbstractValidator<MarkLeftRequest>
{
    public MarkLeftRequestValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(200);
}

/// <summary>
/// One renter on the check-in screen: <c>Inside</c> is green, otherwise red. <c>CanEnter</c> is false when the
/// customer has no working access code (revoked, suspended for unpaid rent, or locked after the move-out deadline).
/// </summary>
public sealed record CheckInBoardRowDto(
    int ContractId, string ContractNumber, string CustomerName, string? CustomerPhone, int StorageUnitId, string UnitCode,
    string FacilityName, bool Inside, int? OpenCheckInId, DateTime? Since, bool CanEnter, CheckOutMethod? LeftHow);

/// <summary>The access code (PIN) the customer tells staff at the desk.</summary>
public sealed record FindByAccessCodeRequest(string AccessCode);

public class FindByAccessCodeRequestValidator : AbstractValidator<FindByAccessCodeRequest>
{
    public FindByAccessCodeRequestValidator() => RuleFor(x => x.AccessCode).NotEmpty().MaximumLength(20);
}

public class CheckInRequestValidator : AbstractValidator<CheckInRequest>
{
    public CheckInRequestValidator()
    {
        RuleFor(x => x.ContractId).GreaterThan(0);
        RuleFor(x => x.Note).MaximumLength(200);
    }
}

public interface ICheckInService
{
    Task<Result<CheckInDto>> CheckInAsync(CheckInRequest request, CancellationToken ct);
    Task<Result<CheckInDto>> CheckOutAsync(int checkInId, CancellationToken ct);
    Task<Result<CheckInDto>> MarkLeftAsync(int checkInId, MarkLeftRequest request, CancellationToken ct);

    /// <summary>The hourly job: closes visits still open at their facility's closing time. Returns how many.</summary>
    Task<int> CloseAtClosingTimeAsync(DateTime nowUtc, CancellationToken ct);
    Task<Result<CheckInDto>> GetAsync(int checkInId, CancellationToken ct);
    Task<Result<PagedResult<CheckInDto>>> ListAsync(int? contractId, int page, int pageSize, CancellationToken ct);
    Task<Result<IReadOnlyList<CheckInBoardRowDto>>> BoardAsync(CancellationToken ct);
    Task<Result<CheckInBoardRowDto>> FindByAccessCodeAsync(FindByAccessCodeRequest request, CancellationToken ct);
}

/// <summary>SR-05: staff record a customer entering and leaving their unit. One open visit per contract at a time.</summary>
public class CheckInService(IUnitOfWork uow, ICurrentUser currentUser, IPasswordHasher hasher) : ICheckInService
{
    /// <summary>Rentals whose customer may still hold a key: everything between handover and closing.</summary>
    private static readonly ContractStatus[] Renting =
        [ContractStatus.Active, ContractStatus.Overdue, ContractStatus.Suspended, ContractStatus.PendingReturn];

    public async Task<Result<CheckInDto>> CheckInAsync(CheckInRequest request, CancellationToken ct)
    {
        var created = await uow.InTransactionAsync<int>(async () =>
        {
            var facilityIds = StaffScope.FacilityIdsOf(uow, currentUser.UserId);
            var contract = await uow.Repository<Contract>().Query()
                .FirstOrDefaultAsync(c => c.ContractId == request.ContractId && facilityIds.Contains(c.StorageUnit.FacilityId), ct);
            if (contract is null)
                return Error.NotFound("Contract not found.");
            // The gate is the access code, not the contract status: a customer moving out (PendingReturn) still comes
            // in, while one locked out for unpaid rent or past the move-out deadline does not.
            if (!await UsableCodes().AnyAsync(a => a.ContractId == contract.ContractId, ct))
                return Error.Conflict("This customer has no working access code; they cannot enter.");
            if (await uow.Repository<CheckIn>().Query().AnyAsync(v => v.ContractId == contract.ContractId && v.CheckOutAt == null, ct))
                return Error.Conflict("This customer is already inside; check them out first.");

            var visit = new CheckIn
            {
                ContractId = contract.ContractId,
                StorageUnitId = contract.StorageUnitId,
                RecordedBy = currentUser.UserId,
                CheckInAt = DateTime.UtcNow,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            };
            uow.Repository<CheckIn>().Add(visit);
            await uow.SaveChangesAsync(ct);
            return visit.CheckInId;
        }, ct);
        return created.IsSuccess ? await GetAsync(created.Value, ct) : created.Error;
    }

    public async Task<Result<CheckInDto>> CheckOutAsync(int checkInId, CancellationToken ct)
    {
        var visit = await Visible().FirstOrDefaultAsync(v => v.CheckInId == checkInId, ct);
        if (visit is null)
            return Error.NotFound("Visit not found.");
        if (visit.CheckOutAt is not null)
            return Error.Conflict("This visit is already checked out.");
        visit.CheckOutAt = DateTime.UtcNow;
        visit.CheckOutMethod = CheckOutMethod.Desk;
        await uow.SaveChangesAsync(ct);
        return await GetAsync(checkInId, ct);
    }

    public async Task<Result<CheckInDto>> MarkLeftAsync(int checkInId, MarkLeftRequest request, CancellationToken ct)
    {
        var visit = await Visible().FirstOrDefaultAsync(v => v.CheckInId == checkInId, ct);
        if (visit is null)
            return Error.NotFound("Visit not found.");
        if (visit.CheckOutAt is not null)
            return Error.Conflict("This visit is already checked out.");
        var leftAt = VietnamTime.ToUtc(request.LeftAt);
        if (leftAt < visit.CheckInAt || leftAt > DateTime.UtcNow.AddMinutes(1))
            return Error.Validation("The leaving time must be after they came in and not in the future.");

        visit.CheckOutAt = leftAt;
        visit.CheckOutMethod = CheckOutMethod.MarkedByStaff;
        visit.CheckOutReason = request.Reason.Trim();
        await uow.SaveChangesAsync(ct);
        return await GetAsync(checkInId, ct);
    }

    public async Task<int> CloseAtClosingTimeAsync(DateTime nowUtc, CancellationToken ct)
    {
        var open = await uow.Repository<CheckIn>().Query().Where(v => v.CheckOutAt == null)
            .Select(v => new { Visit = v, v.StorageUnit.Facility.OpeningHours }).ToListAsync(ct);
        var closed = 0;
        foreach (var row in open)
        {
            var closing = ClosingAfter(row.Visit.CheckInAt, row.OpeningHours);
            if (closing > nowUtc)
                continue;
            row.Visit.CheckOutAt = closing;
            row.Visit.CheckOutMethod = CheckOutMethod.AutoAtClosing;
            closed++;
        }
        await uow.SaveChangesAsync(ct);
        return closed;
    }

    /// <summary>
    /// The facility's first closing time after the customer came in, in UTC. Opening hours read "06:00-22:00";
    /// without them the day ends at 23:59, Vietnam time.
    /// </summary>
    public static DateTime ClosingAfter(DateTime checkInUtc, string? openingHours)
    {
        var close = openingHours?.Split('-') is [_, var end] && TimeOnly.TryParse(end.Trim(), out var t) ? t : new TimeOnly(23, 59);
        var localIn = checkInUtc.AddHours(7);
        var closing = DateOnly.FromDateTime(localIn).ToDateTime(close);
        if (closing <= localIn)
            closing = closing.AddDays(1);
        return DateTime.SpecifyKind(closing.AddHours(-7), DateTimeKind.Utc);
    }

    public async Task<Result<CheckInDto>> GetAsync(int checkInId, CancellationToken ct)
    {
        var dto = await Project(Visible().Where(v => v.CheckInId == checkInId)).FirstOrDefaultAsync(ct);
        return dto is null ? Error.NotFound("Visit not found.") : dto;
    }

    public async Task<Result<PagedResult<CheckInDto>>> ListAsync(int? contractId, int page, int pageSize, CancellationToken ct)
    {
        var query = Visible();
        if (contractId is not null)
            query = query.Where(v => v.ContractId == contractId);
        return await Project(query.OrderByDescending(v => v.CheckInAt)).ToPagedResultAsync(page, pageSize, ct);
    }

    /// <summary>Everyone renting at the facilities this person works at; those inside first.</summary>
    public async Task<Result<IReadOnlyList<CheckInBoardRowDto>>> BoardAsync(CancellationToken ct) =>
        (await BoardRowsAsync(VisibleContracts(), ct)).OrderByDescending(r => r.Inside).ThenBy(r => r.CustomerName).ToList();

    /// <summary>The renter whose working access code matches the PIN they said at the desk.</summary>
    public async Task<Result<CheckInBoardRowDto>> FindByAccessCodeAsync(FindByAccessCodeRequest request, CancellationToken ct)
    {
        var code = request.AccessCode.Trim();
        var candidates = await UsableCodes().Where(a => VisibleContracts().Any(c => c.ContractId == a.ContractId))
            .Select(a => new { a.ContractId, a.CodeHash }).ToListAsync(ct);
        // shortcut: codes are salted hashes, so each one is checked in turn; fine for a facility's few hundred renters,
        // add a keyed lookup hash if a single desk ever serves thousands.
        var match = candidates.FirstOrDefault(a => hasher.Verify(code, a.CodeHash));
        if (match is null)
            return Error.NotFound("No working access code matches. Check the code with the customer.");
        return (await BoardRowsAsync(VisibleContracts().Where(c => c.ContractId == match.ContractId), ct)).Single();
    }

    private IQueryable<AccessCode> UsableCodes()
    {
        var now = DateTime.UtcNow;
        return uow.Repository<AccessCode>().Query().Where(a => a.IsActive && a.SuspendedAt == null && a.ExpiresAt > now);
    }

    private IQueryable<Contract> VisibleContracts()
    {
        var query = uow.Repository<Contract>().Query().Where(c => Renting.Contains(c.Status));
        if (StaffScope.SeesAllFacilities(currentUser.Role))
            return query;
        var facilityIds = StaffScope.FacilityIdsOf(uow, currentUser.UserId);
        return query.Where(c => facilityIds.Contains(c.StorageUnit.FacilityId));
    }

    private async Task<List<CheckInBoardRowDto>> BoardRowsAsync(IQueryable<Contract> contracts, CancellationToken ct)
    {
        var rentals = await contracts.Select(c => new
        {
            c.ContractId, c.ContractNumber, Customer = c.Customer.FullName, Phone = c.Customer.PhoneNumber,
            c.StorageUnitId, c.StorageUnit.UnitCode, Facility = c.StorageUnit.Facility.Name,
        }).ToListAsync(ct);
        var ids = rentals.Select(r => r.ContractId).ToList();
        var visits = uow.Repository<CheckIn>().Query().Where(v => ids.Contains(v.ContractId));
        var open = await visits.Where(v => v.CheckOutAt == null)
            .Select(v => new { v.ContractId, v.CheckInId, v.CheckInAt }).ToListAsync(ct);
        var lastTimes = await visits.Where(v => v.CheckOutAt != null).GroupBy(v => v.ContractId)
            .Select(g => g.Max(v => v.CheckOutAt)).ToListAsync(ct);
        var lastOut = await visits.Where(v => lastTimes.Contains(v.CheckOutAt))
            .Select(v => new { v.ContractId, At = v.CheckOutAt, v.CheckOutMethod }).ToListAsync(ct);
        var withCode = await UsableCodes().Where(a => ids.Contains(a.ContractId)).Select(a => a.ContractId).ToListAsync(ct);

        return rentals.Select(r =>
        {
            var inside = open.FirstOrDefault(v => v.ContractId == r.ContractId);
            var left = lastOut.Where(o => o.ContractId == r.ContractId).MaxBy(o => o.At);
            return new CheckInBoardRowDto(r.ContractId, r.ContractNumber, r.Customer, r.Phone, r.StorageUnitId, r.UnitCode,
                r.Facility, inside is not null, inside?.CheckInId, inside?.CheckInAt ?? left?.At,
                withCode.Contains(r.ContractId), inside is null ? left?.CheckOutMethod : null);
        }).ToList();
    }

    /// <summary>Customers see their own visits, facility staff and managers those at their facilities, admin and business ops all.</summary>
    private IQueryable<CheckIn> Visible()
    {
        var query = uow.Repository<CheckIn>().Query();
        var userId = currentUser.UserId;
        if (currentUser.IsCustomer)
            return query.Where(v => v.Contract.CustomerId == userId);
        if (StaffScope.SeesAllFacilities(currentUser.Role))
            return query;
        var facilityIds = StaffScope.FacilityIdsOf(uow, userId);
        return query.Where(v => facilityIds.Contains(v.StorageUnit.FacilityId));
    }

    private static IQueryable<CheckInDto> Project(IQueryable<CheckIn> source) =>
        source.Select(v => new CheckInDto(v.CheckInId, v.ContractId, v.Contract.ContractNumber, v.Contract.Customer.FullName,
            v.StorageUnitId, v.StorageUnit.UnitCode, v.RecordedBy, v.RecordedByUser.FullName, v.CheckInAt, v.CheckOutAt, v.Note,
            v.CheckOutMethod, v.CheckOutReason));
}
