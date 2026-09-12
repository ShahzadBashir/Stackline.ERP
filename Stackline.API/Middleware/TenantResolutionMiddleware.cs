using Stackline.API.Features.Tenants;

namespace Stackline.API.Middleware;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        this._next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        ITenantConnectionResolver tenantConnectionResolver)
    {
        var tenantIdClaim = context.User.FindFirst("tenant_id")?.Value;

        if (!string.IsNullOrEmpty(tenantIdClaim) && Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            var connectionString = await tenantConnectionResolver.GetConnectionStringAsync(tenantId);

            if (connectionString is null)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Tenant not found or inactive.");
                return;
            }

            tenantContext.SetTenant(tenantId, connectionString);
        }

        await _next(context);
    }
}
