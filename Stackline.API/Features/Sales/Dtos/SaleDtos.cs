namespace Stackline.API.Features.Sales.Dtos;

public sealed record SaleLineRequest(
    Guid ItemId,
    decimal Quantity,
    decimal UnitPrice);

public sealed record CreateSaleRequest(
    Guid CustomerId,
    Guid WarehouseId,
    DateOnly SaleDate,
    string? Notes,
    decimal AmountPaid,
    List<SaleLineRequest> Lines);

public sealed record UpdateSaleRequest(
    Guid CustomerId,
    Guid WarehouseId,
    DateOnly SaleDate,
    string? Notes,
    decimal AmountPaid,
    List<SaleLineRequest> Lines);

public sealed record SaleLineResponse(
    Guid Id,
    Guid ItemId,
    string SKU,
    string ItemName,
    string UnitOfMeasure,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record SaleSummaryResponse(
    Guid Id,
    string SaleNumber,
    Guid CustomerId,
    Guid WarehouseId,
    DateOnly SaleDate,
    string Status,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal UnpaidAmount,
    DateTime CreatedAt);

public sealed record SaleResponse(
    Guid Id,
    string SaleNumber,
    Guid CustomerId,
    Guid WarehouseId,
    DateOnly SaleDate,
    string? Notes,
    string Status,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal UnpaidAmount,
    DateTime? PostedAt,
    DateTime CreatedAt,
    List<SaleLineResponse> Lines);