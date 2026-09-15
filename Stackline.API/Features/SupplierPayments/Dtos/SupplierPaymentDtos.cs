namespace Stackline.API.Features.SupplierPayments.Dtos;

public sealed record CreateSupplierPaymentRequest(
    Guid SupplierId,
    DateOnly PaymentDate,
    decimal Amount,
    string PaymentMethod,
    string? PaymentReference,
    string? Notes);

public sealed record UpdateSupplierPaymentRequest(
    Guid SupplierId,
    DateOnly PaymentDate,
    decimal Amount,
    string PaymentMethod,
    string? PaymentReference,
    string? Notes);

public sealed record SupplierPaymentResponse(
    Guid Id,
    string PaymentNumber,
    Guid SupplierId,
    DateOnly PaymentDate,
    decimal Amount,
    string PaymentMethod,
    string? PaymentReference,
    string? Notes,
    string Status,
    DateTime? PostedAt,
    DateTime CreatedAt);