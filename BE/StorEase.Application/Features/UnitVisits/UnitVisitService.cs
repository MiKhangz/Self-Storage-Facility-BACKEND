using FluentValidation;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.UnitVisits;

public sealed record UnitVisitCheckInRequest(int ContractId, string? Note);

public sealed record UnitVisitDto(
    int UnitVisitId, int ContractId, string ContractNumber, string CustomerName, int StorageUnitId, string UnitCode,
    int RecordedBy, string RecordedByName, DateTime CheckInAt, DateTime? CheckOutAt, string? Note);

public class UnitVisitCheckInRequestValidator : AbstractValidator<UnitVisitCheckInRequest>
{
    public UnitVisitCheckInRequestValidator()
    {
        RuleFor(x => x.ContractId).GreaterThan(0);
        RuleFor(x => x.Note).MaximumLength(200);
    }
}

public interface IUnitVisitService
{
    Task<Result<UnitVisitDto>> CheckInAsync(UnitVisitCheckInRequest request, CancellationToken ct);
    Task<Result<UnitVisitDto>> CheckOutAsync(int unitVisitId, CancellationToken ct);
    Task<Result<UnitVisitDto>> GetAsync(int unitVisitId, CancellationToken ct);
    Task<Result<PagedResult<UnitVisitDto>>> ListAsync(int? contractId, int page, int pageSize, CancellationToken ct);
}

/// <summary>SR-05: staff record a customer entering and leaving their unit. One open visit per contract at a time.</summary>
public class UnitVisitService(IUnitOfWork uow, ICurrentUser currentUser) : IUnitVisitService
{
    public async Task<Result<UnitVisitDto>> CheckInAsync(UnitVisitCheckInRequest request, CancellationToken ct)
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
            if (await uow.Repository<UnitVisit>().Query().AnyAsync(v => v.ContractId == contract.ContractId && v.CheckOutAt == null, ct))
                return Error.Conflict("This customer is already inside; check them out first.");

            var visit = new UnitVisit
            {
                ContractId = contract.ContractId,
                StorageUnitId = contract.StorageUnitId,
                RecordedBy = currentUser.UserId,
                CheckInAt = DateTime.UtcNow,
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
        var visit = await Visible().FirstOrDefaultAsync(v => v.UnitVisitId == unitVisitId, ct);
        if (visit is null)
            return Error.NotFound("Visit not found.");
        if (visit.CheckOutAt is not null)
            return Error.Conflict("This visit is already checked out.");
        visit.CheckOutAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
        return await GetAsync(unitVisitId, ct);
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

    /// <summary>Customers see their own visits, facility staff and managers those at their facilities, admin and business ops all.</summary>
    private IQueryable<UnitVisit> Visible()
    {
        var query = uow.Repository<UnitVisit>().Query();
        var userId = currentUser.UserId;
        if (currentUser.IsCustomer)
            return query.Where(v => v.Contract.CustomerId == userId);
        if (StaffScope.SeesAllFacilities(currentUser.Role))
            return query;
        var facilityIds = StaffScope.FacilityIdsOf(uow, userId);
        return query.Where(v => facilityIds.Contains(v.StorageUnit.FacilityId));
    }

    private static IQueryable<UnitVisitDto> Project(IQueryable<UnitVisit> source) =>
        source.Select(v => new UnitVisitDto(v.UnitVisitId, v.ContractId, v.Contract.ContractNumber, v.Contract.Customer.FullName,
            v.StorageUnitId, v.StorageUnit.UnitCode, v.RecordedBy, v.RecordedByUser.FullName, v.CheckInAt, v.CheckOutAt, v.Note));
}
