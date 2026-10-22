using FluentValidation;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.DepositRefunds;

public sealed record DepositRefundDto(
    int DepositRefundId, DepositRefundSource Source, decimal Amount, RefundMethod? Method, string? RefundAccount,
    DepositRefundStatus Status, DateTime CreatedAt, DateTime? PaidAt, string? PaidByName, string? ReferenceNo,
    int CustomerId, string CustomerName, string CustomerPhone, int FacilityId, string FacilityName,
    int ReservationId, string ReservationCode, int? UnitReturnId);

/// <summary>How the money went back and, for a transfer, the bank's reference.</summary>
public sealed record MarkDepositRefundPaidRequest(RefundMethod Method, string? ReferenceNo);

public class MarkDepositRefundPaidRequestValidator : AbstractValidator<MarkDepositRefundPaidRequest>
{
    public MarkDepositRefundPaidRequestValidator()
    {
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.ReferenceNo).MaximumLength(50);
        RuleFor(x => x.ReferenceNo).NotEmpty().When(x => x.Method == RefundMethod.BankTransfer)
            .WithMessage("A bank transfer needs its transaction reference.");
    }
}

public interface IDepositRefundService
{
    /// <summary>Customers see their own refunds; managers their facilities'; business operations and admin all.</summary>
    Task<Result<PagedResult<DepositRefundDto>>> ListAsync(DepositRefundStatus? status, int page, int pageSize, CancellationToken ct);

    /// <summary>Records that the customer has the money back. 409 if it was already paid.</summary>
    Task<Result<DepositRefundDto>> MarkPaidAsync(int depositRefundId, MarkDepositRefundPaidRequest request, CancellationToken ct);
}

public class DepositRefundService(IUnitOfWork uow, ICurrentUser currentUser) : IDepositRefundService
{
    public async Task<Result<PagedResult<DepositRefundDto>>> ListAsync(
        DepositRefundStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var query = Visible();
        if (status is not null)
            query = query.Where(r => r.Status == status);
        // Oldest pending first: those are the customers who have waited longest for their money.
        return await Project(query.OrderBy(r => r.Status == DepositRefundStatus.Paid).ThenBy(r => r.CreatedAt))
            .ToPagedResultAsync(page, pageSize, ct);
    }

    public async Task<Result<DepositRefundDto>> MarkPaidAsync(
        int depositRefundId, MarkDepositRefundPaidRequest request, CancellationToken ct)
    {
        var refund = await Visible().FirstOrDefaultAsync(r => r.DepositRefundId == depositRefundId, ct);
        if (refund is null)
            return Error.NotFound("Deposit refund not found.");
        if (refund.Status == DepositRefundStatus.Paid)
            return Error.Conflict("This deposit refund is already paid.");

        refund.Status = DepositRefundStatus.Paid;
        refund.Method = request.Method;
        refund.ReferenceNo = string.IsNullOrWhiteSpace(request.ReferenceNo) ? null : request.ReferenceNo.Trim();
        refund.PaidAt = DateTime.UtcNow;
        refund.PaidBy = currentUser.UserId;
        await uow.SaveChangesAsync(ct);
        return await Project(Visible().Where(r => r.DepositRefundId == depositRefundId)).FirstAsync(ct);
    }

    /// <summary>
    /// Records a deposit StorEase now owes back, as Pending, in the caller's transaction (no save). Called by a
    /// refundable cancellation and by a return settlement with a refund.
    /// </summary>
    internal static void Owe(IUnitOfWork uow, Reservation reservation, decimal amount, UnitReturn? unitReturn = null)
    {
        if (amount <= 0)
            return;
        uow.Repository<DepositRefund>().Add(new DepositRefund
        {
            CustomerId = reservation.CustomerId,
            FacilityId = reservation.FacilityId,
            ReservationId = reservation.ReservationId,
            UnitReturnId = unitReturn?.UnitReturnId,
            Source = unitReturn is null ? DepositRefundSource.Cancellation : DepositRefundSource.Return,
            Amount = Money.Round(amount),
            Method = unitReturn?.RefundMethod,
            RefundAccount = unitReturn?.RefundAccount,
            Status = DepositRefundStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        });
    }

    private IQueryable<DepositRefund> Visible()
    {
        var query = uow.Repository<DepositRefund>().Query();
        var userId = currentUser.UserId;
        if (currentUser.IsCustomer)
            return query.Where(r => r.CustomerId == userId);
        if (StaffScope.SeesAllFacilities(currentUser.Role))
            return query;
        var facilityIds = StaffScope.FacilityIdsOf(uow, userId);
        return query.Where(r => facilityIds.Contains(r.FacilityId));
    }

    private static IQueryable<DepositRefundDto> Project(IQueryable<DepositRefund> source) =>
        source.Select(r => new DepositRefundDto(
            r.DepositRefundId, r.Source, r.Amount, r.Method, r.RefundAccount, r.Status, r.CreatedAt, r.PaidAt,
            r.PaidByUser != null ? r.PaidByUser.FullName : null, r.ReferenceNo,
            r.CustomerId, r.Customer.FullName, r.Customer.PhoneNumber, r.FacilityId, r.Facility.Name,
            r.ReservationId, r.Reservation.ReservationCode, r.UnitReturnId));
}
