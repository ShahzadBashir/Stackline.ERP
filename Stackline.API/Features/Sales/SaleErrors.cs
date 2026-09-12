using Stackline.API.Common;

namespace Stackline.API.Features.Sales;

public static class SaleErrors
{
    public static readonly Error NotFound = new(
        "sale.not_found",
        "The sale was not found.",
        ErrorType.NotFound);

    public static readonly Error NotDraft = new(
        "sale.not_draft",
        "Only draft sales can be edited or deleted.",
        ErrorType.Conflict);

    public static readonly Error AlreadyPosted = new(
        "sale.already_posted",
        "This sale has already been posted.",
        ErrorType.Conflict);

    public static readonly Error CustomerUnavailable = new(
        "sale.customer_unavailable",
        "Select an active customer.",
        ErrorType.Validation);

    public static readonly Error WarehouseUnavailable = new(
        "sale.warehouse_unavailable",
        "Select an active warehouse.",
        ErrorType.Validation);

    public static readonly Error LinesRequired = new(
        "sale.lines_required",
        "Add at least one sale item.",
        ErrorType.Validation);

    public static readonly Error DuplicateItem = new(
        "sale.duplicate_item",
        "An item can appear only once in a sale.",
        ErrorType.Validation);

    public static readonly Error ItemUnavailable = new(
        "sale.item_unavailable",
        "One or more selected items are unavailable or inactive.",
        ErrorType.Validation);

    public static readonly Error InvalidQuantity = new(
        "sale.invalid_quantity",
        "Quantity must be greater than zero with at most three decimal places.",
        ErrorType.Validation);

    public static readonly Error InvalidUnitPrice = new(
        "sale.invalid_unit_price",
        "Unit price must be zero or more with at most four decimal places.",
        ErrorType.Validation);

    public static readonly Error InvalidPayment = new(
        "sale.invalid_payment",
        "Amount paid must be between zero and the sale total, with at most two decimal places.",
        ErrorType.Validation);

    public static readonly Error InvalidDate = new(
        "sale.invalid_date",
        "Enter a valid sale date.",
        ErrorType.Validation);

    public static readonly Error InvalidNotes = new(
        "sale.invalid_notes",
        "Notes cannot exceed 2,000 characters.",
        ErrorType.Validation);

    public static readonly Error AmountTooLarge = new(
        "sale.amount_too_large",
        "The quantity, price, or total exceeds the supported limit.",
        ErrorType.Validation);

    public static readonly Error ItemUnitChanged = new(
        "sale.item_unit_changed",
        "An item's unit of measure has changed. Review and save the draft before posting.",
        ErrorType.Conflict);

    public static readonly Error InsufficientStock = new(
        "sale.insufficient_stock",
        "There is insufficient stock for one or more items in the selected warehouse.",
        ErrorType.Conflict);

    public static readonly Error CreditLimitExceeded = new(
        "sale.credit_limit_exceeded",
        "This sale would exceed the customer's credit limit. Increase the payment or review their credit limit.",
        ErrorType.Conflict);
}