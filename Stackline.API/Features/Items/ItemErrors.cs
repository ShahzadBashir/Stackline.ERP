using Stackline.API.Common;

namespace Stackline.API.Features.Items;

public static class ItemErrors
{
    public static readonly Error NotFound = new(
        "item.not_found",
        "The item was not found.",
        ErrorType.NotFound);

    public static readonly Error SKURequired = new(
        "item.sku_required",
        "SKU is required.",
        ErrorType.Validation);

    public static readonly Error SKUExists = new(
        "item.sku_exists",
        "An item with this SKU already exists.",
        ErrorType.Conflict);

    public static readonly Error NameRequired = new(
        "item.name_required",
        "Item name is required.",
        ErrorType.Validation);

    public static readonly Error CategoryNotFound = new(
        "item.category_not_found",
        "The category was not found.",
        ErrorType.Validation);

    public static readonly Error UnitOfMeasureRequired = new(
        "item.unit_of_measure_required",
        "Unit of measure is required.",
        ErrorType.Validation);

    public static readonly Error InvalidCostPrice = new(
        "item.invalid_cost_price",
        "Cost price cannot be negative.",
        ErrorType.Validation);

    public static readonly Error InvalidSalePrice = new(
        "item.invalid_sale_price",
        "Sale price cannot be negative.",
        ErrorType.Validation);

    public static readonly Error InvalidReorderLevel = new(
        "item.invalid_reorder_level",
        "Reorder level cannot be negative.",
        ErrorType.Validation);

    public static readonly Error HasStock = new(
        "item.has_stock",
        "The item cannot be deleted while it has stock on hand.",
        ErrorType.Conflict);
}