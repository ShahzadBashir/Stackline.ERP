using Stackline.API.Common;

namespace Stackline.API.Features.Customers;

public static class CustomerErrors
{
    public static readonly Error NotFound = new(
        "customer.not_found",
        "The customer was not found.",
        ErrorType.NotFound);

    public static readonly Error NameRequired = new(
        "customer.name_required",
        "Customer name is required.",
        ErrorType.Validation);

    public static readonly Error InvalidCreditLimit = new(
        "customer.invalid_credit_limit",
        "Credit limit cannot be negative.",
        ErrorType.Validation);


    public static readonly Error OpeningBalanceLocked = new(
    "customer.opening_balance_locked",
    "Opening balance cannot be changed after sales or receipts have been posted.",
    ErrorType.Conflict);

    public static readonly Error InUse = new(
    "customer.in_use",
    "This customer has sales, receipts, or a non-zero opening balance. Deactivate the customer instead.",
    ErrorType.Conflict);

    public static readonly Error BalanceOutOfRange = new(
    "customer.balance_out_of_range",
    "The customer balance exceeds the supported numeric range.",
    ErrorType.Conflict);
}