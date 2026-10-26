using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.UnitVisits;

public sealed record IssueVisitPinRequest(int ContractId);
public sealed record VisitPinDto(string Pin, DateTime ExpiredAt);
public sealed record VerifyVisitPinRequest(string Pin);
public sealed record UnitVisitCheckInRequest(string Pin, string? Note);
public sealed record UnitVisitDto(
    int UnitVisitId, int ContractId, string ContractNumber, string CustomerName, string CustomerPhone,
    int StorageUnitId, string UnitCode, int RecordedBy, string RecordedByName, DateTime CheckInAt,
    DateTime? CheckOutAt, int? CheckedOutBy, string? CheckedOutByName, string? Note);

public sealed record VisitPinIdentityDto(int ContractId, string ContractNumber, string CustomerName,
    string CustomerPhone, string? CitizenId, string UnitCode, string FacilityName, string Purpose);

public sealed class UnitVisitCheckInRequestValidator : AbstractValidator<UnitVisitCheckInRequest>
{
    public UnitVisitCheckInRequestValidator()
    {
        RuleFor(x => x.Pin).Matches("^\\d{6}$");
        RuleFor(x => x.Note).MaximumLength(200);
    }
}

public sealed class VerifyVisitPinRequestValidator : AbstractValidator<VerifyVisitPinRequest>
{
    public VerifyVisitPinRequestValidator() => RuleFor(x => x.Pin).Matches("^\\d{6}$");
}

public interface IUnitVisitService
{
    Task<Result<VisitPinDto>> IssueCheckInPinAsync(int contractId, CancellationToken ct);
    Task<Result<VisitPinIdentityDto>> VerifyCheckInPinAsync(string pin, CancellationToken ct);
    Task<Result<UnitVisitDto>> CheckInAsync(UnitVisitCheckInRequest request, CancellationToken ct);
    Task<Result<UnitVisitDto>> CheckOutAsync(int unitVisitId, CancellationToken ct);
    Task<Result<UnitVisitDto>> GetAsync(int unitVisitId, CancellationToken ct);
    Task<Result<PagedResult<UnitVisitDto>>> ListAsync(int? contractId, int page, int pageSize, CancellationToken ct);
}

/// <summary>Customer issued, three minute PINs are single use; staff consume them to record gate events.</summary>
public sealed class UnitVisitService(IUnitOfWork uow, ICurrentUser currentUser) : IUnitVisitService
{
    private const string CheckInPurpose = "CheckIn";
    private static readonly TimeSpan PinLifetime = TimeSpan.FromMinutes(3);

    public Task<Result<VisitPinDto>> IssueCheckInPinAsync(int contractId, CancellationToken ct) =>
        IssuePinAsync(contractId, ct);

    private async Task<Result<VisitPinDto>> IssuePinAsync(int contractId, CancellationToken ct)
    {
        if (!currentUser.IsCustomer)
            return Error.Forbidden("Only the customer can request a visit PIN.");

        var contract = await uow.Repository<Contract>().Query().AsNoTracking()
            .FirstOrDefaultAsync(c => c.ContractId == contractId && c.CustomerId == currentUser.UserId, ct);
        if (contract is null)
            return Error.NotFound("Contract not found.");
        if (contract.Status != ContractStatus.Active)
            return Error.Conflict("A visit PIN is available only for an Active contract.");
        if (await uow.Repository<UnitVisit>().Query()
                .AnyAsync(v => v.ContractId == contractId && v.CheckOutAt == null, ct))
            return Error.Conflict("You are already checked in.");

        var now = DateTime.UtcNow;
        var code = CreatePin();
        while (await uow.Repository<UnitVisitPin>().Query().AnyAsync(p => p.CodeHash == HashPin(code) && !p.IsUsed && p.ExpiredAt > now, ct))
            code = CreatePin();

        var pin = new UnitVisitPin
        {
            UserId = currentUser.UserId,
            ContractId = contractId,
            Purpose = CheckInPurpose,
            CodeHash = HashPin(code),
            CreatedAt = now,
            ExpiredAt = now.Add(PinLifetime),
            IsUsed = false,
        };
        uow.Repository<UnitVisitPin>().Add(pin);
        await uow.SaveChangesAsync(ct);
        return new VisitPinDto(code, pin.ExpiredAt);
    }

    public Task<Result<VisitPinIdentityDto>> VerifyCheckInPinAsync(string pin, CancellationToken ct) =>
        VerifyPinAsync(pin, CheckInPurpose, ct);

    private async Task<Result<VisitPinIdentityDto>> VerifyPinAsync(string pin, string purpose, CancellationToken ct)
    {
        if (!IsGateStaff())
            return Error.Forbidden("Only facility staff can verify a visit PIN.");
        var pinHash = HashPin(pin);
        var now = DateTime.UtcNow;
        var record = await VisiblePins().AsNoTracking()
            .Where(p => p.CodeHash == pinHash && p.Purpose == purpose && !p.IsUsed && p.ExpiredAt > now)
            .Select(p => new { p.ContractId, p.Contract.ContractNumber, p.User.FullName, p.User.PhoneNumber,
                p.User.CitizenId, p.Contract.StorageUnit.UnitCode, p.Contract.StorageUnit.Facility.Name,
                p.UnitVisitId })
            .FirstOrDefaultAsync(ct);
        if (record is null)
            return Error.NotFound("PIN is invalid, expired, already used, or outside your facility.");
        if (purpose == CheckInPurpose && await uow.Repository<UnitVisit>().Query()
                .AnyAsync(v => v.ContractId == record.ContractId && v.CheckOutAt == null, ct))
            return Error.Conflict("This customer is already checked in.");
        return new VisitPinIdentityDto(record.ContractId, record.ContractNumber, record.FullName,
            record.PhoneNumber, record.CitizenId, record.UnitCode, record.Name, purpose);
    }

    public async Task<Result<UnitVisitDto>> CheckInAsync(UnitVisitCheckInRequest request, CancellationToken ct)
    {
        if (!IsGateStaff())
            return Error.Forbidden("Only facility staff can check in a customer.");

        var created = await uow.InTransactionAsync<int>(async () =>
        {
            var now = DateTime.UtcNow;
            var hash = HashPin(request.Pin);
            var pin = await VisiblePins().FirstOrDefaultAsync(p => p.CodeHash == hash && p.Purpose == CheckInPurpose
                && !p.IsUsed && p.ExpiredAt > now, ct);
            if (pin is null)
                return Error.NotFound("PIN is invalid, expired, already used, or outside your facility.");
            var contract = await uow.Repository<Contract>().Query()
                .FirstOrDefaultAsync(c => c.ContractId == pin.ContractId && c.Status == ContractStatus.Active, ct);
            if (contract is null)
                return Error.Conflict("The customer's contract is no longer Active.");
            if (await uow.Repository<UnitVisit>().Query().AnyAsync(v => v.ContractId == contract.ContractId && v.CheckOutAt == null, ct))
                return Error.Conflict("This customer is already checked in.");

            var outstandingPins = await uow.Repository<UnitVisitPin>().Query().Where(p => p.ContractId == contract.ContractId
                && p.Purpose == CheckInPurpose && !p.IsUsed).ToListAsync(ct);
            foreach (var outstandingPin in outstandingPins)
                outstandingPin.IsUsed = true;
            pin.IsUsed = true;
            var visit = new UnitVisit
            {
                ContractId = contract.ContractId,
                StorageUnitId = contract.StorageUnitId,
                RecordedBy = currentUser.UserId,
                CheckInAt = now,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            };
            uow.Repository<UnitVisit>().Add(visit);
            await uow.SaveChangesAsync(ct);
            return visit.UnitVisitId;
        }, ct);
        return created.IsSuccess ? await GetAsync(created.Value, ct) : created.Error;
    }

    public async Task<Result<UnitVisitDto>> CheckOutAsync(int unitVisitId, CancellationToken ct)
    {
        if (!IsGateStaff())
            return Error.Forbidden("Only facility staff can check out a customer.");
        var result = await uow.InTransactionAsync<bool>(async () =>
        {
            var visit = await Visible().FirstOrDefaultAsync(v => v.UnitVisitId == unitVisitId, ct);
            if (visit is null)
                return Error.NotFound("Visit not found.");
            if (visit.CheckOutAt is not null)
                return Error.Conflict("This visit is already checked out.");
            visit.CheckOutAt = DateTime.UtcNow;
            visit.CheckedOutBy = currentUser.UserId;
            await uow.SaveChangesAsync(ct);
            return true;
        }, ct);
        return result.IsSuccess ? await GetAsync(unitVisitId, ct) : result.Error;
    }

    public async Task<Result<UnitVisitDto>> GetAsync(int unitVisitId, CancellationToken ct)
    {
        var dto = await Project(Visible().Where(v => v.UnitVisitId == unitVisitId)).FirstOrDefaultAsync(ct);
        return dto is null ? Error.NotFound("Visit not found.") : dto;
    }

    public async Task<Result<PagedResult<UnitVisitDto>>> ListAsync(int? contractId, int page, int pageSize, CancellationToken ct)
    {
        var query = Visible();
        if (contractId is not null)
            query = query.Where(v => v.ContractId == contractId);
        return await Project(query.OrderByDescending(v => v.CheckInAt)).ToPagedResultAsync(page, pageSize, ct);
    }

    private bool IsGateStaff() => !currentUser.IsCustomer;

    private IQueryable<UnitVisit> Visible()
    {
        var query = uow.Repository<UnitVisit>().Query();
        if (currentUser.IsCustomer)
            return query.Where(v => v.Contract.CustomerId == currentUser.UserId);
        if (StaffScope.SeesAllFacilities(currentUser.Role))
            return query;
        var facilityIds = StaffScope.FacilityIdsOf(uow, currentUser.UserId);
        return query.Where(v => facilityIds.Contains(v.StorageUnit.FacilityId));
    }

    private IQueryable<UnitVisitPin> VisiblePins()
    {
        var query = uow.Repository<UnitVisitPin>().Query();
        if (StaffScope.SeesAllFacilities(currentUser.Role))
            return query;
        var facilityIds = StaffScope.FacilityIdsOf(uow, currentUser.UserId);
        return query.Where(p => facilityIds.Contains(p.Contract.StorageUnit.FacilityId));
    }

    private static IQueryable<UnitVisitDto> Project(IQueryable<UnitVisit> source) =>
        source.Select(v => new UnitVisitDto(v.UnitVisitId, v.ContractId, v.Contract.ContractNumber,
            v.Contract.Customer.FullName, v.Contract.Customer.PhoneNumber, v.StorageUnitId, v.StorageUnit.UnitCode,
            v.RecordedBy, v.RecordedByUser.FullName, v.CheckInAt, v.CheckOutAt, v.CheckedOutBy,
            v.CheckedOutByUser == null ? null : v.CheckedOutByUser.FullName, v.Note));

    private static string CreatePin() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    private static string HashPin(string pin) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pin)));
}
