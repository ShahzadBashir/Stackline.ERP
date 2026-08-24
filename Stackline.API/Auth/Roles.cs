namespace Stackline.API.Auth;

public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Owner = "Owner";
    public const string Manager = "Manager";
    public const string Staff = "Staff";

    public static readonly string[] TenantRoles = { Owner, Manager, Staff };

    public static readonly string[] CanManageUsers = { Owner };
}
