using Microsoft.EntityFrameworkCore;
using Stackline.API.Data;
using Stackline.API.Data.Entities;

namespace Stackline.API.Auth;

public interface ITenantProvisioningService
{
    Task<Tenant> ProvisionTenantAsync(string companyName, string ownerFullName, string ownerEmail, string ownerPassword);
}

public class TenantProvisioningService : ITenantProvisioningService
{
    private readonly MasterDbContext _masterDb;
    private readonly IPasswordService _passwordService;
    private readonly IConfiguration _config;

    public TenantProvisioningService(MasterDbContext masterDb, IPasswordService passwordService, IConfiguration config)
    {
        _masterDb = masterDb;
        _passwordService = passwordService;
        _config = config;
    }

    public async Task<Tenant> ProvisionTenantAsync(
        string companyName, string ownerFullName, string ownerEmail, string ownerPassword)
    {
        if (await _masterDb.GlobalUsers.AnyAsync(u => u.Email == ownerEmail))
            throw new InvalidOperationException("A user with this email already exists.");

        var tenantId = Guid.NewGuid();
        var dbName = $"tenant_{tenantId:N}";

        var template = _config["Db:TenantTemplateConnectionString"]!;
        var tenantConnectionString = string.Format(template, dbName);

        var tenant = new Tenant
        {
            Id = tenantId,
            CompanyName = companyName,
            DbConnectionString = tenantConnectionString,
            SubscriptionStatus = SubscriptionStatuses.Trial
        };

        // We haven't built AppDbContext yet in this project, so tenant DB
        // creation/migration is commented out for now - see note below.
        // var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        // optionsBuilder.UseNpgsql(tenantConnectionString);
        // await using (var tenantDb = new AppDbContext(optionsBuilder.Options))
        // {
        //     await tenantDb.Database.MigrateAsync();
        // }

        _masterDb.Tenants.Add(tenant);
        _masterDb.GlobalUsers.Add(new GlobalUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FullName = ownerFullName,
            Email = ownerEmail,
            PasswordHash = _passwordService.HashPassword(ownerPassword),
            Role = Roles.Owner
        });

        await _masterDb.SaveChangesAsync();

        return tenant;
    }
}
