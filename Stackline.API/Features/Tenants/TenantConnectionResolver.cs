using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Stackline.API.Data;

namespace Stackline.API.Features.Tenants;

public interface ITenantConnectionResolver
{
    Task<string?> GetConnectionStringAsync(Guid tenantId);
}

public class TenantConnectionResolver : ITenantConnectionResolver
{
    private readonly MasterDbContext _masterDb;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);
    public TenantConnectionResolver(MasterDbContext masterDb, IMemoryCache cache)
    {
        _masterDb = masterDb;
        _cache = cache;
    }

    public async Task<string?> GetConnectionStringAsync(Guid tenantId)
    {
        var cacheKey = $"tenant-conn:{tenantId}";

        if (_cache.TryGetValue(cacheKey, out string? cached))
            return cached;

        var tenant = await _masterDb.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive);

        if (tenant is null)
            return null;

        _cache.Set(cacheKey, tenant.DbConnectionString, CacheDuration);
        return tenant.DbConnectionString;
    }
}
