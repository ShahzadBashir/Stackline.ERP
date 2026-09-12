namespace Stackline.API.Purchases.Dtos;

public sealed record PurchaseLineRequest(
    Guid ItemId,
    decimal Quantity,
    decimal UnitCost);

public sealed record CreatePurchaseRequest(
    Guid SupplierId,
    Guid WarehouseId,
    DateOnly PurchaseDate,
    string? SupplierInvoiceNumber,
    string? Notes,
    List<PurchaseLineRequest> Lines);

public sealed record UpdatePurchaseRequest(
    Guid SupplierId,
    Guid WarehouseId,
    DateOnly PurchaseDate,
    string? SupplierInvoiceNumber,
    string? Notes,
    List<PurchaseLineRequest> Lines);

public sealed record PurchaseLineResponse(
    Guid Id,
    Guid ItemId,
    string SKU,
    string ItemName,
    string UnitOfMeasure,
    decimal Quantity,
    decimal UnitCost,
    decimal LineTotal);

public sealed record PurchaseSummaryResponse(
    Guid Id,
    string PurchaseNumber,
    Guid SupplierId,
    Guid WarehouseId,
    DateOnly PurchaseDate,
    string Status,
    decimal TotalAmount,
    DateTime CreatedAt);

public sealed record PurchaseResponse(
    Guid Id,
    string PurchaseNumber,
    Guid SupplierId,
    Guid WarehouseId,
    DateOnly PurchaseDate,
    string? SupplierInvoiceNumber,
    string? Notes,
    string Status,
    decimal TotalAmount,
    DateTime? PostedAt,
    DateTime CreatedAt,
    List<PurchaseLineResponse> Lines);