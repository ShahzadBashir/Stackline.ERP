using Stackline.API.Common;

namespace Stackline.API.Features.Warehouses;

public static class WarehouseErrors
{
    public static readonly Error NotFound = new(
        "warehouse.not_found",
        "The warehouse was not found.",
        ErrorType.NotFound);

    public static readonly Error HasStock = new(
        "warehouse.has_stock",
        "This warehouse still has stock. Transfer or adjust its stock before deleting it.",
        ErrorType.Conflict);
}