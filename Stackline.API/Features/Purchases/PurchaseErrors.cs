using Stackline.API.Common;

namespace Stackline.API.Features.Purchases;

public static class PurchaseErrors
{
    public static readonly Error NotFound = new(
        "purchase.not_found",
        "The purchase was not found.",
        ErrorType.NotFound);

    public static readonly Error NotDraft = new(
        "purchase.not_draft",
        "Only draft purchases can be edited or deleted.",
        ErrorType.Conflict);

    public static readonly Error AlreadyPosted = new(
        "purchase.already_posted",
        "This purchase has already been posted.",
        ErrorType.Conflict);

    public static readonly Error SupplierUnavailable = new(
        "purchase.supplier_unavailable",
        "Select an active supplier.",
        ErrorType.Validation);

    public static readonly Error WarehouseUnavailable = new(
        "purchase.warehouse_unavailable",
        "Select an active warehouse.",
        ErrorType.Validation);

    public static readonly Error LinesRequired = new(
        "purchase.lines_required",
        "Add at least one purchase item.",
        ErrorType.Validation);

    public static readonly Error DuplicateItem = new(
        "purchase.duplicate_item",
        "An item can appear only once in a purchase.",
        ErrorType.Validation);

    public static readonly Error ItemUnavailable = new(
        "purchase.item_unavailable",
        "One or more selected items are unavailable or inactive.",
        ErrorType.Validation);

    public static readonly Error InvalidQuantity = new(
        "purchase.invalid_quantity",
        "Quantity must be greater than zero with at most three decimal places.",
        ErrorType.Validation);

    public static readonly Error InvalidUnitCost = new(
        "purchase.invalid_unit_cost",
        "Unit cost must be zero or more with at most four decimal places.",
        ErrorType.Validation);

    public static readonly Error InvalidDate = new(
        "purchase.invalid_date",
        "Enter a valid purchase date.",
        ErrorType.Validation);

    public static readonly Error InvalidDetails = new(
        "purchase.invalid_details",
        "Invoice number or notes exceed the allowed length.",
        ErrorType.Validation);

    public static readonly Error AmountTooLarge = new(
    "purchase.amount_too_large",
    "The quantity, unit cost, or purchase total exceeds the supported limit.",
    ErrorType.Validation);

    public static readonly Error ItemUnitChanged = new(
        "purchase.item_unit_changed",
        "An item's unit of measure has changed. Review and save the draft before posting.",
        ErrorType.Conflict);

    public static readonly Error StockUnavailable = new(
        "purchase.stock_unavailable",
        "An existing stock record is inactive or deleted. Resolve it before posting.",
        ErrorType.Conflict);
}