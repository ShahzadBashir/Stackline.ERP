namespace Stackline.API.Features.Inventory.Dtos;

public sealed record InventoryPage<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record StockBalanceResponse(
    Guid Id,
    Guid ItemId,
    string SKU,
    string ItemName,
    string UnitOfMeasure,
    Guid WarehouseId,
    string WarehouseName,
    decimal QuantityOnHand,
    int ReorderLevel,
    bool IsLowStock,
    DateTime LastUpdated);

public sealed record StockMovementResponse(
    Guid Id,
    Guid ItemId,
    string SKU,
    string ItemName,
    Guid WarehouseId,
    string WarehouseName,
    string MovementType,
    decimal Quantity,
    string ReferenceType,
    Guid ReferenceId,
    DateTime CreatedAt);