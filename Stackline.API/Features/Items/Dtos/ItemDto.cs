namespace Stackline.API.Features.Items.Dtos;

public sealed record CreateItemRequest(
    string SKU,
    string Name,
    Guid CategoryId,
    string UnitOfMeasure = "Pcs",
    decimal CostPrice = 0,
    decimal SalePrice = 0,
    int ReorderLevel = 0);

public sealed record UpdateItemRequest(
    string SKU,
    string Name,
    Guid CategoryId,
    string UnitOfMeasure,
    decimal CostPrice,
    decimal SalePrice,
    int ReorderLevel,
    bool IsActive);

public sealed record ItemResponse(
    Guid Id,
    string SKU,
    string Name,
    Guid CategoryId,
    string UnitOfMeasure,
    decimal CostPrice,
    decimal SalePrice,
    int ReorderLevel,
    bool IsActive,
    DateTime CreatedAt);