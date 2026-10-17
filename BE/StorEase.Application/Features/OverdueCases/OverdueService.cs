using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.OverdueCases;

public sealed record OverdueCaseDto(
    int OverdueCaseId, int ContractId, string ContractNumber, int CustomerId, string CustomerName, string UnitCode,
    int? InvoiceId, string? InvoiceNumber, decimal? Outstanding, DateOnly OverdueSince, int DaysOverdue,
    decimal LateFeeAmount, OverdueStage Stage, DateTime? AccessSuspendedAt, DateTime? ResolvedAt);

public interface IOverdueService
{
    Task<Result<PagedResult<OverdueCaseDto>>> ListAsync(OverdueStage? stage, bool openOnly, int page, int pageSize, CancellationToken ct);
    Task<Result<OverdueCaseDto>> GetAsync(int overdueCaseId, CancellationToken ct);

    /// <summary>A manager locks out early, before the policy's automatic lockout day.</summary>
    Task<Result<OverdueCaseDto>> LockOutAsync(int overdueCaseId, CancellationToken ct);

    /// <summary>A manager restores access while the invoice is still unpaid, e.g. after a payment promise.</summary>
    Task<Result<OverdueCaseDto>> RestoreAccessAsync(int overdueCaseId, CancellationToken ct);
}

public sealed class OverdueService(IUnitOfWork uow, ICurrentUser currentUser) : IOverdueService
{
    public async Task<Result<PagedResult<OverdueCaseDto>>> ListAsync(
        OverdueStage? stage, bool openOnly, int page, int pageSize, CancellationToken ct)
    {
        var query = Visible();
        if (stage is not null)
            query = query.Where(c => c.Stage == stage);
        if (openOnly)
            query = query.Where(c => c.ResolvedAt == null);
        return await Project(query.OrderByDescending(c => c.DaysOverdue).ThenBy(c => c.OverdueCaseId))
            .ToPagedResultAsync(page, pageSize, ct);
    }

    public async Task<Result<OverdueCaseDto>> GetAsync(int overdueCaseId, CancellationToken ct)
    {
        var dto = await Project(Visible().Where(c => c.OverdueCaseId == overdueCaseId)).FirstOrDefaultAsync(ct);
        return dto is null ? Error.NotFound("Overdue case not found.") : dto;
    }

    public async Task<Result<OverdueCaseDto>> LockOutAsync(int overdueCaseId, CancellationToken ct)
    {
        var done = await uow.InTransactionAsync<int>(async () =>
        {
            var overdue = await Visible().FirstOrDefaultAsync(c => c.OverdueCaseId == overdueCaseId, ct);
            if (overdue is null)
                return Error.NotFound("Overdue case not found.");
            if (overdue.ResolvedAt is not null || overdue.Stage is OverdueStage.Suspended or OverdueStage.ClearOut or OverdueStage.Resolved)
                return Error.Conflict($"This case is already {overdue.Stage}.");
            await OverdueRules.LockOutAsync(uow, overdue, DateTime.UtcNow, ct);
            return overdue.OverdueCaseId;
        }, ct);
        return done.IsSuccess ? await GetAsync(done.Value, ct) : done.Error;
    }

    public async Task<Result<OverdueCaseDto>> RestoreAccessAsync(int overdueCaseId, CancellationToken ct)
    {
        var done = await uow.InTransactionAsync<int>(async () =>
        {
            var overdue = await Visible().FirstOrDefaultAsync(c => c.OverdueCaseId == overdueCaseId, ct);
            if (overdue is null)
                return Error.NotFound("Overdue case not found.");
            if (overdue.ResolvedAt is not null || overdue.Stage is not (OverdueStage.Suspended or OverdueStage.ClearOut))
                return Error.Conflict("Access is not suspended for this case.");
            // The case stays open at the late-fee stage. AccessSuspendedAt is kept, which tells the daily job not to
            // lock out again, so the manager owns the follow-up.
            await OverdueRules.LiftAsync(uow, overdue.ContractId, DateTime.UtcNow, ct);
            var contract = await uow.Repository<Contract>().FindAsync(overdue.ContractId, ct);
            contract!.Status = ContractStatus.Overdue;
            overdue.Stage = OverdueStage.LateFee;
            return overdue.OverdueCaseId;
        }, ct);
        return done.IsSuccess ? await GetAsync(done.Value, ct) : done.Error;
    }

    private IQueryable<OverdueCase> Visible()
    {
        var query = uow.Repository<OverdueCase>().Query();
        if (StaffScope.SeesAllFacilities(currentUser.Role))
            return query;
        var facilityIds = StaffScope.FacilityIdsOf(uow, currentUser.UserId);
        return query.Where(c => facilityIds.Contains(c.Contract.StorageUnit.FacilityId));
    }

    private static IQueryable<OverdueCaseDto> Project(IQueryable<OverdueCase> query) => query.Select(c => new OverdueCaseDto(
        c.OverdueCaseId, c.ContractId, c.Contract.ContractNumber, c.Contract.CustomerId, c.Contract.Customer.FullName,
        c.Contract.StorageUnit.UnitCode, c.InvoiceId, c.Invoice != null ? c.Invoice.InvoiceNumber : null,
        c.Invoice != null ? c.Invoice.TotalAmount - c.Invoice.PaidAmount : null,
        c.OverdueSince, c.DaysOverdue, c.LateFeeAmount, c.Stage, c.AccessSuspendedAt, c.ResolvedAt));
}
