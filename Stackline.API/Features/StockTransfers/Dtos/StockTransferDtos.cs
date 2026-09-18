namespace Stackline.API.Features.StockTransfers.Dtos;

public sealed record SaveStockTransferRequest(
    Guid SourceWarehouseId,
    Guid DestinationWarehouseId,
    DateOnly TransferDate,
    string? Notes,
    List<StockTransferLineRequest> Lines);

public sealed record StockTransferLineRequest(
    Guid ItemId,
    decimal Quantity);

public sealed record StockTransferSummaryResponse(
    Guid Id,
    string TransferNumber,
    Guid SourceWarehouseId,
    Guid DestinationWarehouseId,
    DateOnly TransferDate,
    string Status,
    int LineCount,
    DateTime CreatedAt);

public sealed record StockTransferResponse(
    Guid Id,
    string TransferNumber,
    Guid SourceWarehouseId,
    Guid DestinationWarehouseId,
    DateOnly TransferDate,
    string? Notes,
    string Status,
    DateTime? PostedAt,
    DateTime CreatedAt,
    List<StockTransferLineResponse> Lines);

public sealed record StockTransferLineResponse(
    Guid Id,
    Guid ItemId,
    string SKU,
    string ItemName,
    string UnitOfMeasure,
    decimal Quantity);