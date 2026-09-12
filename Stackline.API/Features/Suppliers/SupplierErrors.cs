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
}