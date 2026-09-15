namespace Stackline.API.Features.CustomerReceipts.Dtos;

public sealed record CreateCustomerReceiptRequest(
    Guid CustomerId,
    DateOnly ReceiptDate,
    decimal Amount,
    string PaymentMethod,
    string? PaymentReference,
    string? Notes);

public sealed record UpdateCustomerReceiptRequest(
    Guid CustomerId,
    DateOnly ReceiptDate,
    decimal Amount,
    string PaymentMethod,
    string? PaymentReference,
    string? Notes);

public sealed record CustomerReceiptResponse(
    Guid Id,
    string ReceiptNumber,
    Guid CustomerId,
    DateOnly ReceiptDate,
    decimal Amount,
    string PaymentMethod,
    string? PaymentReference,
    string? Notes,
    string Status,
    DateTime? PostedAt,
    DateTime CreatedAt);