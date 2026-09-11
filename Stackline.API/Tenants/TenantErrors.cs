using Stackline.API.Common;

namespace Stackline.API.Tenants;

public static class TenantErrors
{
    public static readonly Error NotFound = new(
        "tenant.not_found",
        "The tenant was not found.",
        ErrorType.NotFound);

    public static readonly Error OwnerEmailExists = new(
        "tenant.owner_email_exists",
        "A user with this email already exists.",
        ErrorType.Conflict);
}