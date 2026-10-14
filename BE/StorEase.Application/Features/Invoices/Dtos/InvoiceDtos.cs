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
