using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Invoices.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Invoices;

public interface IInvoiceService
{
    Task<Result<PagedResult<InvoiceDto>>> ListAsync(InvoiceStatus? status, int page, int pageSize, CancellationToken ct);
    Task<Result<InvoiceDto>> GetAsync(int invoiceId, CancellationToken ct);
    Task<Result<InvoiceDto>> PayOnlineAsync(int invoiceId, OnlinePaymentRequest request, CancellationToken ct);
    Task<Result<InvoiceDto>> RecordManualPaymentAsync(int invoiceId, ManualPaymentRequest request, CancellationToken ct);
}

public sealed class InvoiceService(IUnitOfWork uow, ICurrentUser currentUser) : IInvoiceService
{
    public async Task<Result<PagedResult<InvoiceDto>>> ListAsync(InvoiceStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, PagingExtensions.MaxPageSize);
        var query = Visible().AsNoTracking();
        if (status is not null)
            query = query.Where(i => i.Status == status);
        var total = await query.CountAsync(ct);
        var invoices = await query.OrderByDescending(i => i.InvoiceId)
            .Skip((safePage - 1) * safePageSize).Take(safePageSize)
            .ToListAsync(ct);
        return new PagedResult<InvoiceDto>(invoices.Select(ToDto).ToList(), safePage, safePageSize, total);
    }

    public async Task<Result<InvoiceDto>> GetAsync(int invoiceId, CancellationToken ct)
    {
        var invoice = await Visible().AsNoTracking().FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct);
        return invoice is null ? Error.NotFound("Invoice not found.") : ToDto(invoice);
    }

    public Task<Result<InvoiceDto>> PayOnlineAsync(int invoiceId, OnlinePaymentRequest request, CancellationToken ct) =>
        CaptureAsync(invoiceId, request.Amount, PaymentMethod.Gateway, request.ReferenceNo, null, ct);

    public Task<Result<InvoiceDto>> RecordManualPaymentAsync(int invoiceId, ManualPaymentRequest request, CancellationToken ct) =>
        CaptureAsync(invoiceId, request.Amount, PaymentMethod.Cash, request.ReferenceNo, currentUser.UserId, ct);

    private async Task<Result<InvoiceDto>> CaptureAsync(
        int invoiceId, decimal amount, PaymentMethod method, string? referenceNo, int? receivedBy, CancellationToken ct)
    {
        var captured = await uow.InTransactionAsync<int>(async () =>
        {
            var invoice = await Visible().Include(i => i.Payments).FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct);
            if (invoice is null)
                return Error.NotFound("Invoice not found.");
            if (invoice.Status is InvoiceStatus.Paid or InvoiceStatus.WrittenOff)
                return Error.Conflict("This invoice is already closed.");

            var outstanding = Money(invoice.TotalAmount - invoice.PaidAmount);
            if (amount > outstanding)
                return Error.Conflict($"Payment exceeds the outstanding amount of {outstanding:0.00}.");

            var payment = new Payment
            {
                InvoiceId = invoice.InvoiceId,
                ReceivedBy = receivedBy,
                PaymentCode = await NewPaymentCodeAsync(ct),
                Method = method,
                Amount = Money(amount),
                PaidAt = DateTime.UtcNow,
                ReferenceNo = string.IsNullOrWhiteSpace(referenceNo) ? null : referenceNo.Trim(),
                Status = PaymentStatus.Succeeded,
            };
            uow.Repository<Payment>().Add(payment);
            invoice.PaidAmount = Money(invoice.PaidAmount + payment.Amount);
            invoice.Status = invoice.PaidAmount >= invoice.TotalAmount ? InvoiceStatus.Paid : InvoiceStatus.Issued;
            return invoice.InvoiceId;
        }, ct);
        return captured.IsSuccess ? await GetAsync(captured.Value, ct) : captured.Error;
    }

    private IQueryable<Invoice> Visible()
    {
        var query = uow.Repository<Invoice>().Query()
            .Include(i => i.InvoiceItems)
            .Include(i => i.Payments)
            .Include(i => i.Contract).ThenInclude(c => c!.StorageUnit)
            .Include(i => i.Reservation);

        if (currentUser.IsCustomer)
            return query.Where(i => i.CustomerId == currentUser.UserId);
        if (StaffScope.SeesAllFacilities(currentUser.Role))
            return query;
        if (currentUser.Role is Roles.FacilityStaff or Roles.FacilityManager)
        {
            var facilityIds = StaffScope.FacilityIdsOf(uow, currentUser.UserId);
            return query.Where(i =>
                (i.Contract != null && facilityIds.Contains(i.Contract.StorageUnit.FacilityId))
                || (i.Reservation != null && facilityIds.Contains(i.Reservation.FacilityId)));
        }
        return query.Where(_ => false);
    }

    private static InvoiceDto ToDto(Invoice invoice) => new(
        invoice.InvoiceId,
        invoice.ContractId,
        invoice.ReservationId,
        invoice.CustomerId,
        invoice.InvoiceNumber,
        invoice.IssueDate,
        invoice.DueDate,
        invoice.TotalAmount,
        invoice.PaidAmount,
        Money(invoice.TotalAmount - invoice.PaidAmount),
        invoice.Status,
        invoice.InvoiceItems.OrderBy(i => i.InvoiceItemId)
            .Select(i => new InvoiceItemDto(i.InvoiceItemId, i.FeeTypeId, i.Description, i.Quantity, i.UnitAmount, i.LineAmount)).ToList(),
        invoice.Payments.OrderByDescending(p => p.PaymentId)
            .Select(p => new InvoicePaymentDto(p.PaymentId, p.PaymentCode, p.Method, p.Amount, p.PaidAt, p.ReferenceNo, p.Status)).ToList());

    private async Task<string> NewPaymentCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = $"PY{DateTime.UtcNow:yyMMddHHmmss}{Random.Shared.Next(1000):D3}";
            if (!await uow.Repository<Payment>().Query().AnyAsync(p => p.PaymentCode == code, ct))
                return code;
        }
        throw new InvalidOperationException("Could not generate a unique payment code.");
    }

    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
