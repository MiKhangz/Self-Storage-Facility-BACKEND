using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.Invoices.Dtos;
using StorEase.Application.Features.OverdueCases;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Invoices;

public interface IInvoiceService
{
    Task<Result<PagedResult<InvoiceDto>>> ListAsync(InvoiceStatus? status, int page, int pageSize, CancellationToken ct);
    Task<Result<InvoiceDto>> GetAsync(int invoiceId, CancellationToken ct);

    /// <summary>The customer starts paying their own invoice; nothing is credited until the gateway confirms.</summary>
    Task<Result<PaymentIntentDto>> PayOnlineAsync(int invoiceId, PaymentRequest request, CancellationToken ct, string? clientIp = null);

    /// <summary>The gateway's signed callback: credits the invoice on success. Idempotent.</summary>
    Task<Result<InvoiceDto>> ConfirmGatewayAsync(GatewayCallbackRequest request, CancellationToken ct);
    Task<Result<InvoiceDto>> ConfirmVnPayAsync(IReadOnlyDictionary<string, string> parameters, CancellationToken ct);

    /// <summary>Development only: the customer stands in for the gateway's success callback.</summary>
    Task<Result<InvoiceDto>> SimulateGatewaySuccessAsync(string paymentCode, CancellationToken ct);

    Task<Result<InvoiceDto>> RecordManualPaymentAsync(int invoiceId, PaymentRequest request, CancellationToken ct);
    Task<Result<InvoiceDto>> AdjustAsync(int invoiceId, InvoiceAdjustmentRequest request, CancellationToken ct);
    Task<Result<InvoiceDto>> RefundPaymentAsync(int paymentId, RefundRequest request, CancellationToken ct);
    Task<Result<PagedResult<PaymentHistoryDto>>> ListPaymentsAsync(int? invoiceId, PaymentStatus? status, int page, int pageSize, CancellationToken ct);
}

public sealed class InvoiceService(IUnitOfWork uow, ICurrentUser currentUser, PaymentGatewayOptions gateway) : IInvoiceService
{
    public async Task<Result<PagedResult<InvoiceDto>>> ListAsync(InvoiceStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var query = Visible().AsNoTracking();
        if (status is not null)
            query = query.Where(i => i.Status == status);
        var paged = await query.OrderByDescending(i => i.InvoiceId).ToPagedResultAsync(page, pageSize, ct);
        return new PagedResult<InvoiceDto>(paged.Items.Select(ToDto).ToList(), paged.Page, paged.PageSize, paged.TotalCount);
    }

    public async Task<Result<InvoiceDto>> GetAsync(int invoiceId, CancellationToken ct)
    {
        var invoice = await Visible().AsNoTracking().FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct);
        return invoice is null ? Error.NotFound("Invoice not found.") : ToDto(invoice);
    }

    public async Task<Result<PaymentIntentDto>> PayOnlineAsync(
        int invoiceId, PaymentRequest request, CancellationToken ct, string? clientIp = null)
    {
        var invoice = await Visible().FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct);
        if (invoice is null)
            return Error.NotFound("Invoice not found.");
        if (invoice.Status is InvoiceStatus.Paid or InvoiceStatus.WrittenOff)
            return Error.Conflict("This invoice is already closed.");
        var outstanding = Money.Round(invoice.TotalAmount - invoice.PaidAmount);
        if (request.Amount > outstanding)
            return Error.Conflict($"Payment exceeds the outstanding amount of {outstanding:0.00}.");

        var payment = new Payment
        {
            InvoiceId = invoice.InvoiceId,
            PaymentCode = await NewPaymentCodeAsync(ct),
            Method = PaymentMethod.Gateway,
            Amount = Money.Round(request.Amount),
            ReferenceNo = Clean(request.ReferenceNo),
            Status = PaymentStatus.Pending,
        };
        uow.Repository<Payment>().Add(payment);
        await uow.SaveChangesAsync(ct);
        var checkoutUrl = gateway.IsVnPayConfigured
            ? VnPayGateway.BuildPaymentUrl(gateway, payment.PaymentCode, payment.Amount, clientIp)
            : $"{gateway.CheckoutBaseUrl}?payment={Uri.EscapeDataString(payment.PaymentCode)}";
        return new PaymentIntentDto(payment.PaymentId, payment.PaymentCode, invoice.InvoiceId, payment.Amount, payment.Status,
            checkoutUrl);
    }

    public async Task<Result<InvoiceDto>> ConfirmGatewayAsync(GatewayCallbackRequest request, CancellationToken ct)
    {
        if (!SignatureMatches(request))
            return Error.Unauthorized("Invalid gateway signature.");
        return await SettleGatewayPaymentAsync(request.PaymentCode, request.Status, request.Amount, Clean(request.ReferenceNo), ct);
    }

    public async Task<Result<InvoiceDto>> ConfirmVnPayAsync(
        IReadOnlyDictionary<string, string> parameters, CancellationToken ct)
    {
        if (!gateway.IsVnPayConfigured)
            return Error.NotImplemented("VNPay is not configured for this environment.");
        if (!VnPayGateway.TryReadCallback(gateway, parameters, out var paymentCode, out var status, out var amount, out var referenceNo))
            return Error.Unauthorized("Invalid VNPay callback signature or payload.");
        return await SettleGatewayPaymentAsync(paymentCode, status, amount, Clean(referenceNo), ct);
    }

    public async Task<Result<InvoiceDto>> SimulateGatewaySuccessAsync(string paymentCode, CancellationToken ct)
    {
        if (!gateway.AllowSimulation)
            return Error.NotFound("Payment simulation is not available.");
        var payment = await uow.Repository<Payment>().Query().Where(p => p.PaymentCode == paymentCode)
            .Select(p => new { p.Amount, p.Invoice.CustomerId }).FirstOrDefaultAsync(ct);
        if (payment is null || (currentUser.IsCustomer && payment.CustomerId != currentUser.UserId))
            return Error.NotFound("Payment not found.");
        return await SettleGatewayPaymentAsync(paymentCode, PaymentStatus.Succeeded, payment.Amount, $"SIM-{paymentCode}", ct);
    }

    public Task<Result<InvoiceDto>> RecordManualPaymentAsync(int invoiceId, PaymentRequest request, CancellationToken ct) =>
        CaptureAsync(invoiceId, request.Amount, PaymentMethod.Cash, request.ReferenceNo, currentUser.UserId, ct);

    public async Task<Result<InvoiceDto>> AdjustAsync(int invoiceId, InvoiceAdjustmentRequest request, CancellationToken ct)
    {
        var done = await uow.InTransactionAsync<int>(async () =>
        {
            var invoice = await uow.Repository<Invoice>().Query().Include(i => i.InvoiceItems)
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct);
            if (invoice is null)
                return Error.NotFound("Invoice not found.");
            if (invoice.Status == InvoiceStatus.WrittenOff)
                return Error.Conflict("A written-off invoice cannot be adjusted.");
            var newTotal = Money.Round(invoice.TotalAmount + request.Amount);
            if (newTotal < 0)
                return Error.Validation("The adjustment would make the invoice total negative.");
            if (newTotal < invoice.PaidAmount)
                return Error.Conflict("The customer has already paid more than the adjusted total; refund a payment first.");

            invoice.InvoiceItems.Add(InvoiceBook.Line(request.Description.Trim(), request.Amount));
            InvoiceBook.Retotal(invoice, VietnamTime.Today);
            await OverdueRules.ResolvePaidAsync(uow, invoice, DateTime.UtcNow, ct);
            return invoice.InvoiceId;
        }, ct);
        return done.IsSuccess ? await GetAsync(done.Value, ct) : done.Error;
    }

    public async Task<Result<InvoiceDto>> RefundPaymentAsync(int paymentId, RefundRequest request, CancellationToken ct)
    {
        var done = await uow.InTransactionAsync<int>(async () =>
        {
            var payment = await uow.Repository<Payment>().Query().Include(p => p.Invoice).ThenInclude(i => i.InvoiceItems)
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId, ct);
            if (payment is null)
                return Error.NotFound("Payment not found.");
            if (payment.Status != PaymentStatus.Succeeded)
                return Error.Conflict($"Only a Succeeded payment can be refunded; this one is {payment.Status}.");

            payment.Status = PaymentStatus.Refunded;
            if (!string.IsNullOrWhiteSpace(request.Reason))
            {
                var note = $"REFUND: {request.Reason.Trim()}";
                payment.ReferenceNo = note.Length > 50 ? note[..50] : note;
            }
            var invoice = payment.Invoice;
            invoice.PaidAmount = Money.Round(invoice.PaidAmount - payment.Amount);
            // A refunded deposit of a cancelled reservation stays closed; anything else reopens for the balance.
            if (invoice.Status != InvoiceStatus.WrittenOff)
            {
                invoice.Status = InvoiceStatus.Issued;
                InvoiceBook.Retotal(invoice, VietnamTime.Today);
                if (invoice.ReservationId is { } reservationId && invoice.ContractId is null
                    && await uow.Repository<Reservation>().Query().AnyAsync(r => r.ReservationId == reservationId
                        && (r.Status == ReservationStatus.Cancelled || r.Status == ReservationStatus.Expired), ct))
                    invoice.Status = InvoiceStatus.WrittenOff;
            }
            return invoice.InvoiceId;
        }, ct);
        return done.IsSuccess ? await GetAsync(done.Value, ct) : done.Error;
    }

    public async Task<Result<PagedResult<PaymentHistoryDto>>> ListPaymentsAsync(
        int? invoiceId, PaymentStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var visibleInvoices = Visible().Select(i => i.InvoiceId);
        var query = uow.Repository<Payment>().Query().Where(p => visibleInvoices.Contains(p.InvoiceId));
        if (invoiceId is not null)
            query = query.Where(p => p.InvoiceId == invoiceId);
        if (status is not null)
            query = query.Where(p => p.Status == status);

        return await query.OrderByDescending(p => p.PaymentId)
            .Select(p => new PaymentHistoryDto(p.PaymentId, p.PaymentCode, p.InvoiceId, p.Invoice.InvoiceNumber, p.Invoice.CustomerId,
                p.Method, p.Amount, p.PaidAt, p.ReferenceNo, p.Status, p.ReceivedByUser != null ? p.ReceivedByUser.FullName : null))
            .ToPagedResultAsync(page, pageSize, ct);
    }

    private async Task<Result<InvoiceDto>> SettleGatewayPaymentAsync(
        string paymentCode, PaymentStatus outcome, decimal amount, string? referenceNo, CancellationToken ct)
    {
        Result<int> settled;
        try
        {
            settled = await uow.InTransactionAsync<int>(async () =>
            {
                var payment = await uow.Repository<Payment>().Query()
                    .Include(p => p.Invoice)
                    .FirstOrDefaultAsync(p => p.PaymentCode == paymentCode && p.Method == PaymentMethod.Gateway, ct);
                if (payment is null)
                    return Error.NotFound("Payment not found.");
                if (payment.Status == outcome)
                    return payment.InvoiceId; // the gateway retries callbacks; repeating one changes nothing
                if (payment.Status != PaymentStatus.Pending)
                    return Error.Conflict($"The payment is already {payment.Status}.");
                if (Money.Round(amount) != payment.Amount)
                    return Error.Conflict("The confirmed amount does not match the payment.");

                payment.ReferenceNo = referenceNo ?? payment.ReferenceNo;
                if (outcome == PaymentStatus.Failed)
                {
                    payment.Status = PaymentStatus.Failed;
                    return payment.InvoiceId;
                }

                // The gateway has taken the money, so it is always credited, even if the invoice was paid at the
                // counter in the meantime; PaidAmount then exceeds the total and Business Operations refund this payment.
                var invoice = payment.Invoice;
                payment.Status = PaymentStatus.Succeeded;
                payment.PaidAt = DateTime.UtcNow;
                invoice.PaidAmount = Money.Round(invoice.PaidAmount + payment.Amount);
                if (invoice.PaidAmount >= invoice.TotalAmount)
                    invoice.Status = InvoiceStatus.Paid;
                await OverdueRules.ResolvePaidAsync(uow, invoice, DateTime.UtcNow, ct);
                return invoice.InvoiceId;
            }, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Error.Conflict("The invoice was updated by another payment. Retry the callback.");
        }
        if (!settled.IsSuccess)
            return settled.Error;

        // The callback is anonymous, so read the result without the caller's visibility filter.
        var invoiceDto = await Query().AsNoTracking().FirstAsync(i => i.InvoiceId == settled.Value, ct);
        return ToDto(invoiceDto);
    }

    private async Task<Result<InvoiceDto>> CaptureAsync(
        int invoiceId, decimal amount, PaymentMethod method, string? referenceNo, int? receivedBy, CancellationToken ct)
    {
        Result<int> captured;
        try
        {
            captured = await uow.InTransactionAsync<int>(async () =>
            {
                var invoice = await Visible().Include(i => i.Payments).FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct);
                if (invoice is null)
                    return Error.NotFound("Invoice not found.");
                if (invoice.Status is InvoiceStatus.Paid or InvoiceStatus.WrittenOff)
                    return Error.Conflict("This invoice is already closed.");

                var outstanding = Money.Round(invoice.TotalAmount - invoice.PaidAmount);
                if (amount > outstanding)
                    return Error.Conflict($"Payment exceeds the outstanding amount of {outstanding:0.00}.");

                var payment = new Payment
                {
                    InvoiceId = invoice.InvoiceId,
                    ReceivedBy = receivedBy,
                    PaymentCode = await NewPaymentCodeAsync(ct),
                    Method = method,
                    Amount = Money.Round(amount),
                    PaidAt = DateTime.UtcNow,
                    ReferenceNo = Clean(referenceNo),
                    Status = PaymentStatus.Succeeded,
                };
                uow.Repository<Payment>().Add(payment);
                invoice.PaidAmount = Money.Round(invoice.PaidAmount + payment.Amount);
                if (invoice.PaidAmount >= invoice.TotalAmount)
                    invoice.Status = InvoiceStatus.Paid;
                await OverdueRules.ResolvePaidAsync(uow, invoice, DateTime.UtcNow, ct);
                return invoice.InvoiceId;
            }, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // PaidAmount is a concurrency token: a parallel payment (e.g. a double-click) already changed the balance.
            return Error.Conflict("The invoice was updated by another payment. Reload and try again.");
        }
        return captured.IsSuccess ? await GetAsync(captured.Value, ct) : captured.Error;
    }

    /// <summary>Exposed for the simulated gateway and tests: the signature a genuine callback must carry.</summary>
    public static string Sign(string secret, string paymentCode, PaymentStatus status, decimal amount, string? referenceNo)
    {
        var payload = $"{paymentCode}|{status}|{amount.ToString("0.00", CultureInfo.InvariantCulture)}|{referenceNo ?? ""}";
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private bool SignatureMatches(GatewayCallbackRequest request)
    {
        if (string.IsNullOrEmpty(gateway.Secret))
            return false;
        var expected = Sign(gateway.Secret, request.PaymentCode, request.Status, request.Amount, Clean(request.ReferenceNo));
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(request.Signature.ToLowerInvariant()));
    }

    private IQueryable<Invoice> Query() => uow.Repository<Invoice>().Query()
        .Include(i => i.InvoiceItems)
        .Include(i => i.Payments)
        .Include(i => i.Contract).ThenInclude(c => c!.StorageUnit)
        .Include(i => i.Reservation);

    private IQueryable<Invoice> Visible()
    {
        var query = Query();
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
        Money.Round(invoice.TotalAmount - invoice.PaidAmount),
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

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
