using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Stackline.API.Auth;
using Stackline.API.Data;
using Stackline.API.Tenants.Dtos;

namespace Stackline.API.Tenants;

public static class TenantEndpoints
{
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tenants")
            .WithTags("Tenants")
            .RequireAuthorization(new AuthorizeAttribute { Roles = Roles.SuperAdmin });

        group.MapGet("/", async (MasterDbContext db, CancellationToken ct) =>
        {
            var tenants = await db.Tenants
                .AsNoTracking()
                .Select(t => new TenantResponse(t.Id, t.CompanyName, t.SubscriptionStatus, t.IsActive, t.CreatedAt))
                .ToListAsync(ct);

            return Results.Ok(tenants);
        });

        group.MapGet("/{id:guid}", async (Guid id, MasterDbContext db, CancellationToken ct) =>
        {
            var tenant = await db.Tenants
                .AsNoTracking()
                .Where(t => t.Id == id)
                .Select(t => new TenantResponse(t.Id, t.CompanyName, t.SubscriptionStatus, t.IsActive, t.CreatedAt))
                .FirstOrDefaultAsync(ct);

            return tenant is not null ? Results.Ok(tenant) : Results.NotFound();
        });

        group.MapPost("/", async (
            CreateTenantRequest request,
            ITenantProvisioningService provisioningService,
            CancellationToken ct) =>
        {
            var tenant = await provisioningService.ProvisionTenantAsync(
                request.CompanyName, request.OwnerFullName, request.OwnerEmail, request.OwnerPassword);

            return Results.Created($"/api/tenants/{tenant.Id}",
                new TenantResponse(tenant.Id, tenant.CompanyName, tenant.SubscriptionStatus, tenant.IsActive, tenant.CreatedAt));
        });

        group.MapDelete("/{id:guid}", async (Guid id, MasterDbContext db, CancellationToken ct) =>
        {
            var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);
            if (tenant is null) return Results.NotFound();

            tenant.IsDeleted = true;
            tenant.IsActive = false;
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        });

        return app;
    }
}