namespace Stackline.API.Features.Tenants;

public interface ITenantContext
{
    Guid TenantId { get; }
    string ConnectionString { get; }
    bool IsResolved { get; }
    void SetTenant(Guid tenantId, string connectionString);
}

public class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; }
    public string ConnectionString { get; private set; } = string.Empty;
    public bool IsResolved { get; private set; }

    public void SetTenant(Guid tenantId, string connectionString)
    {
        TenantId = tenantId;
        ConnectionString = connectionString;
        IsResolved = true;
    }
}