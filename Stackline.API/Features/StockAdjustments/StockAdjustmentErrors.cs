using Stackline.API.Common;

namespace Stackline.API.Features.StockAdjustments;

public static class StockAdjustmentErrors
{
    public static readonly Error NotFound = new(
        "stock_adjustment.not_found",
        "The stock adjustment was not found.",
        ErrorType.NotFound);

    public static readonly Error NotDraft = new(
        "stock_adjustment.not_draft",
        "Only draft stock adjustments can be changed or deleted.",
        ErrorType.Conflict);

    public static readonly Error AlreadyPosted = new(
        "stock_adjustment.already_posted",
        "This stock adjustment has already been posted.",
        ErrorType.Conflict);

    public static readonly Error InvalidDate = new(
        "stock_adjustment.invalid_date",
        "Enter a valid adjustment date.",
        ErrorType.Validation);

    public static readonly Error InvalidReason = new(
        "stock_adjustment.invalid_reason",
        "Select a valid adjustment reason.",
        ErrorType.Validation);

    public static readonly Error InvalidNotes = new(
        "stock_adjustment.invalid_notes",
        "Notes cannot exceed 2,000 characters.",
        ErrorType.Validation);

    public static readonly Error NotesRequired = new(
        "stock_adjustment.notes_required",
        "Enter notes explaining the adjustment when the reason is Other.",
        ErrorType.Validation);

    public static readonly Error LinesRequired = new(
        "stock_adjustment.lines_required",
        "Add at least one valid item line.",
        ErrorType.Validation);

    public static readonly Error DuplicateItem = new(
        "stock_adjustment.duplicate_item",
        "An item can appear only once in an adjustment.",
        ErrorType.Validation);

    public static readonly Error InvalidDirection = new(
        "stock_adjustment.invalid_direction",
        "Select Increase or Decrease for each item.",
        ErrorType.Validation);

    public static readonly Error DirectionNotAllowed = new(
        "stock_adjustment.direction_not_allowed",
        "The selected direction is not allowed for this adjustment reason.",
        ErrorType.Validation);

    public static readonly Error InvalidQuantity = new(
        "stock_adjustment.invalid_quantity",
        "Quantity must be positive, within the supported range, and have no more than three decimal places.",
        ErrorType.Validation);

    public static readonly Error WarehouseUnavailable = new(
        "stock_adjustment.warehouse_unavailable",
        "The selected warehouse is inactive, deleted, or unavailable.",
        ErrorType.Conflict);

    public static readonly Error ItemUnavailable = new(
        "stock_adjustment.item_unavailable",
        "One or more items are inactive, deleted, or unavailable.",
        ErrorType.Conflict);

    public static readonly Error ItemUnitChanged = new(
        "stock_adjustment.item_unit_changed",
        "An item's unit of measure has changed. Review and save the draft before posting.",
        ErrorType.Conflict);

    public static readonly Error StockUnavailable = new(
        "stock_adjustment.stock_unavailable",
        "An existing stock record is inactive or deleted and cannot be adjusted.",
        ErrorType.Conflict);

    public static readonly Error InsufficientStock = new(
        "stock_adjustment.insufficient_stock",
        "There is insufficient stock for one or more decreases in the selected warehouse.",
        ErrorType.Conflict);

    public static readonly Error StockOutOfRange = new(
        "stock_adjustment.stock_out_of_range",
        "The resulting stock quantity is outside the supported range.",
        ErrorType.Conflict);
}