namespace Stackline.API.Data.Entities;

public class Tenant : BaseEntity
{
    public string CompanyName { get; set; } = string.Empty;
    public string DbConnectionString { get; set; } = string.Empty;
    public string SubscriptionStatus { get; set; } = SubscriptionStatuses.Trial;

    public ICollection<GlobalUser> Users { get; set; }
        = new List<GlobalUser>();
}

public class GlobalUser : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = Auth.Roles.Staff;
    public Guid? TenantId { get; set; }
    public Tenant? Tenant { get; set; }
}
