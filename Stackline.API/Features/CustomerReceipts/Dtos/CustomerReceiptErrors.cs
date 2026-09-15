using Stackline.API.Common;

namespace Stackline.API.Features.CustomerReceipts;

public static class CustomerReceiptErrors
{
    public static readonly Error NotFound = new(
        "customer_receipt.not_found",
        "The customer receipt was not found.",
        ErrorType.NotFound);

    public static readonly Error NotDraft = new(
        "customer_receipt.not_draft",
        "Only draft receipts can be edited or deleted.",
        ErrorType.Conflict);

    public static readonly Error AlreadyPosted = new(
        "customer_receipt.already_posted",
        "This receipt has already been posted.",
        ErrorType.Conflict);

    public static readonly Error CustomerUnavailable = new(
        "customer_receipt.customer_unavailable",
        "The customer was not found or has been deleted.",
        ErrorType.Validation);

    public static readonly Error InvalidDate = new(
        "customer_receipt.invalid_date",
        "Enter a valid receipt date.",
        ErrorType.Validation);

    public static readonly Error InvalidAmount = new(
        "customer_receipt.invalid_amount",
        "Receipt amount must be greater than zero with at most two decimal places.",
        ErrorType.Validation);

    public static readonly Error AmountTooLarge = new(
        "customer_receipt.amount_too_large",
        "Receipt amount exceeds the supported limit.",
        ErrorType.Validation);

    public static readonly Error InvalidPaymentMethod = new(
        "customer_receipt.invalid_payment_method",
        "Select Cash, BankTransfer, or Card.",
        ErrorType.Validation);

    public static readonly Error InvalidDetails = new(
        "customer_receipt.invalid_details",
        "Payment reference cannot exceed 100 characters and notes cannot exceed 2,000 characters.",
        ErrorType.Validation);
}