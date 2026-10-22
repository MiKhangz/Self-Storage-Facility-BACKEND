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
    int RecordedBy, string RecordedByName, DateTime CheckInAt, DateTime? CheckOutAt, string? Note);

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
    Task<Result<CheckInDto>> GetAsync(int checkInId, CancellationToken ct);
    Task<Result<PagedResult<CheckInDto>>> ListAsync(int? contractId, int page, int pageSize, CancellationToken ct);
}

/// <summary>SR-05: staff record a customer entering and leaving their unit. One open visit per contract at a time.</summary>
public class CheckInService(IUnitOfWork uow, ICurrentUser currentUser) : ICheckInService
{
    public async Task<Result<CheckInDto>> CheckInAsync(CheckInRequest request, CancellationToken ct)
    {
        var created = await uow.InTransactionAsync<int>(async () =>
        {
            var facilityIds = StaffScope.FacilityIdsOf(uow, currentUser.UserId);
            var contract = await uow.Repository<Contract>().Query()
                .FirstOrDefaultAsync(c => c.ContractId == request.ContractId && facilityIds.Contains(c.StorageUnit.FacilityId), ct);
            if (contract is null)
                return Error.NotFound("Contract not found.");
            if (contract.Status != ContractStatus.Active)
                return Error.Conflict($"Only an Active contract may visit its unit; this one is {contract.Status}.");
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
        await uow.SaveChangesAsync(ct);
        return await GetAsync(checkInId, ct);
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
            v.StorageUnitId, v.StorageUnit.UnitCode, v.RecordedBy, v.RecordedByUser.FullName, v.CheckInAt, v.CheckOutAt, v.Note));
}
