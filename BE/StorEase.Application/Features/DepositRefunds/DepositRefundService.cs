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
    int ReservationId, string ReservationCode, int? HandoverId);

public interface IDepositRefundService
{
    /// <summary>Customers see their own refunds; managers their facilities'; business operations and admin all.</summary>
    Task<Result<PagedResult<DepositRefundDto>>> ListAsync(DepositRefundStatus? status, int page, int pageSize, CancellationToken ct);
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
        return await Project(query.OrderBy(r => r.Status).ThenBy(r => r.CreatedAt)).ToPagedResultAsync(page, pageSize, ct);
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
            r.ReservationId, r.Reservation.ReservationCode, r.HandoverId));
}
