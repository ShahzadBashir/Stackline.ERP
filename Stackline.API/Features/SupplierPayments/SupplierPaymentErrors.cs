using Stackline.API.Common;

namespace Stackline.API.Features.SupplierPayments;

public static class SupplierPaymentErrors
{
    public static readonly Error NotFound = new(
        "supplier_payment.not_found",
        "The supplier payment was not found.",
        ErrorType.NotFound);

    public static readonly Error NotDraft = new(
        "supplier_payment.not_draft",
        "Only draft payments can be edited or deleted.",
        ErrorType.Conflict);

    public static readonly Error AlreadyPosted = new(
        "supplier_payment.already_posted",
        "This payment has already been posted.",
        ErrorType.Conflict);

    public static readonly Error SupplierUnavailable = new(
        "supplier_payment.supplier_unavailable",
        "The supplier was not found or has been deleted.",
        ErrorType.Validation);

    public static readonly Error InvalidDate = new(
        "supplier_payment.invalid_date",
        "Enter a valid payment date.",
        ErrorType.Validation);

    public static readonly Error InvalidAmount = new(
        "supplier_payment.invalid_amount",
        "Payment amount must be greater than zero with at most two decimal places.",
        ErrorType.Validation);

    public static readonly Error AmountTooLarge = new(
        "supplier_payment.amount_too_large",
        "Payment amount exceeds the supported limit.",
        ErrorType.Validation);

    public static readonly Error InvalidPaymentMethod = new(
        "supplier_payment.invalid_payment_method",
        "Select Cash, BankTransfer, or Card.",
        ErrorType.Validation);

    public static readonly Error InvalidDetails = new(
        "supplier_payment.invalid_details",
        "Payment reference cannot exceed 100 characters and notes cannot exceed 2,000 characters.",
        ErrorType.Validation);
}