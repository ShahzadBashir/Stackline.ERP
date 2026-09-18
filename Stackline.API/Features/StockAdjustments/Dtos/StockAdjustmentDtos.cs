namespace Stackline.API.Features.StockAdjustments.Dtos;

// Create and update accept the same draft fields.
public sealed record SaveStockAdjustmentRequest(
    Guid WarehouseId,
    DateOnly AdjustmentDate,
    string Reason,
    string? Notes,
    List<StockAdjustmentLineRequest> Lines);

public sealed record StockAdjustmentLineRequest(
    Guid ItemId,
    string Direction,
    decimal Quantity);

public sealed record StockAdjustmentSummaryResponse(
    Guid Id,
    string AdjustmentNumber,
    Guid WarehouseId,
    DateOnly AdjustmentDate,
    string Reason,
    string Status,
    int LineCount,
    DateTime CreatedAt);

public sealed record StockAdjustmentResponse(
    Guid Id,
    string AdjustmentNumber,
    Guid WarehouseId,
    DateOnly AdjustmentDate,
    string Reason,
    string? Notes,
    string Status,
    DateTime? PostedAt,
    DateTime CreatedAt,
    List<StockAdjustmentLineResponse> Lines);

public sealed record StockAdjustmentLineResponse(
    Guid Id,
    Guid ItemId,
    string SKU,
    string ItemName,
    string UnitOfMeasure,
    string Direction,
    decimal Quantity);