using Stackline.API.Common;

namespace Stackline.API.Features.StockTransfers;

public static class StockTransferErrors
{
    public static readonly Error NotFound = new(
        "stock_transfer.not_found",
        "The stock transfer was not found.",
        ErrorType.NotFound);

    public static readonly Error NotDraft = new(
        "stock_transfer.not_draft",
        "Only draft stock transfers can be changed or deleted.",
        ErrorType.Conflict);

    public static readonly Error AlreadyPosted = new(
        "stock_transfer.already_posted",
        "This stock transfer has already been posted.",
        ErrorType.Conflict);

    public static readonly Error InvalidDate = new(
        "stock_transfer.invalid_date",
        "Enter a valid transfer date.",
        ErrorType.Validation);

    public static readonly Error InvalidNotes = new(
        "stock_transfer.invalid_notes",
        "Notes cannot exceed 2,000 characters.",
        ErrorType.Validation);

    public static readonly Error WarehousesRequired = new(
        "stock_transfer.warehouses_required",
        "Select both source and destination warehouses.",
        ErrorType.Validation);

    public static readonly Error SameWarehouse = new(
        "stock_transfer.same_warehouse",
        "Source and destination warehouses must be different.",
        ErrorType.Validation);

    public static readonly Error WarehouseUnavailable = new(
        "stock_transfer.warehouse_unavailable",
        "A selected warehouse is inactive, deleted, or unavailable.",
        ErrorType.Conflict);

    public static readonly Error LinesRequired = new(
        "stock_transfer.lines_required",
        "Add at least one valid item line.",
        ErrorType.Validation);

    public static readonly Error DuplicateItem = new(
        "stock_transfer.duplicate_item",
        "An item can appear only once in a transfer.",
        ErrorType.Validation);

    public static readonly Error InvalidQuantity = new(
        "stock_transfer.invalid_quantity",
        "Quantity must be positive, within the supported range, and have no more than three decimal places.",
        ErrorType.Validation);

    public static readonly Error ItemUnavailable = new(
        "stock_transfer.item_unavailable",
        "One or more items are inactive, deleted, or unavailable.",
        ErrorType.Conflict);

    public static readonly Error ItemUnitChanged = new(
        "stock_transfer.item_unit_changed",
        "An item's unit of measure has changed. Review and save the draft before posting.",
        ErrorType.Conflict);

    public static readonly Error StockUnavailable = new(
        "stock_transfer.stock_unavailable",
        "An existing stock record is inactive or deleted and cannot be used for this transfer.",
        ErrorType.Conflict);

    public static readonly Error InsufficientStock = new(
        "stock_transfer.insufficient_stock",
        "There is insufficient stock for one or more items in the source warehouse.",
        ErrorType.Conflict);

    public static readonly Error StockOutOfRange = new(
        "stock_transfer.stock_out_of_range",
        "A stock quantity is outside the supported range, or the transfer would exceed that range.",
        ErrorType.Conflict);
}