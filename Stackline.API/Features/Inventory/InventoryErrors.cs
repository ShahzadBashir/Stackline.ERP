using Stackline.API.Common;

namespace Stackline.API.Features.Inventory;

public static class InventoryErrors
{
    public static readonly Error InvalidPagination = new(
        "inventory.invalid_pagination",
        "Page must be positive and page size must be between 1 and 100.",
        ErrorType.Validation);
}