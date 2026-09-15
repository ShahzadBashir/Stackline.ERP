using Stackline.API.Common;

namespace Stackline.API.Features.Suppliers;

public static class SupplierErrors
{
    public static readonly Error NotFound = new(
        "supplier.not_found",
        "The supplier was not found.",
        ErrorType.NotFound);

    public static readonly Error NameRequired = new(
        "supplier.name_required",
        "Supplier name is required.",
        ErrorType.Validation);

    public static readonly Error BalanceOutOfRange = new(
    "supplier.balance_out_of_range",
    "The supplier balance exceeds the supported numeric range.",
    ErrorType.Conflict);

    public static readonly Error OpeningBalanceLocked = new(
        "supplier.opening_balance_locked",
        "Opening balance cannot be changed after purchases or payments have been posted.",
        ErrorType.Conflict);

    public static readonly Error InUse = new(
        "supplier.in_use",
        "This supplier has purchases, payments, or a non-zero opening balance. Deactivate the supplier instead.",
        ErrorType.Conflict);
}