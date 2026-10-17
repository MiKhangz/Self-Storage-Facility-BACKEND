using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Invoices.Dtos;

public sealed record InvoiceItemDto(
    int InvoiceItemId,
    int? FeeTypeId,
    string Description,
    int Quantity,
    decimal UnitAmount,
    decimal LineAmount);

public sealed record InvoicePaymentDto(
    int PaymentId,
    string PaymentCode,
    PaymentMethod Method,
    decimal Amount,
    DateTime? PaidAt,
    string? ReferenceNo,
    PaymentStatus Status);

public sealed record InvoiceDto(
    int InvoiceId,
    int? ContractId,
    int? ReservationId,
    int CustomerId,
    string InvoiceNumber,
    DateOnly IssueDate,
    DateOnly DueDate,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    InvoiceStatus Status,
    IReadOnlyList<InvoiceItemDto> Items,
    IReadOnlyList<InvoicePaymentDto> Payments);

public sealed record PaymentRequest(decimal Amount, string? ReferenceNo);

/// <summary>
/// A started online payment. It stays <c>Pending</c> and does not count toward the invoice until the gateway
/// confirms it through the signed callback.
/// </summary>
public sealed record PaymentIntentDto(int PaymentId, string PaymentCode, int InvoiceId, decimal Amount, PaymentStatus Status, string CheckoutUrl);

/// <summary>
/// Sent by the payment gateway. <see cref="Signature"/> is lowercase hex HMAC-SHA256 with the shared secret over
/// <c>{PaymentCode}|{Status}|{Amount:0.00}|{ReferenceNo}</c> (invariant culture, empty string for no reference).
/// </summary>
public sealed record GatewayCallbackRequest(string PaymentCode, PaymentStatus Status, decimal Amount, string? ReferenceNo, string Signature);

public sealed record PaymentHistoryDto(
    int PaymentId, string PaymentCode, int InvoiceId, string InvoiceNumber, int CustomerId, PaymentMethod Method,
    decimal Amount, DateTime? PaidAt, string? ReferenceNo, PaymentStatus Status, string? ReceivedByName);

/// <summary>A positive amount charges more (e.g. damage), a negative one credits the customer.</summary>
public sealed record InvoiceAdjustmentRequest(string Description, decimal Amount);

public sealed record RefundRequest(string? Reason);
