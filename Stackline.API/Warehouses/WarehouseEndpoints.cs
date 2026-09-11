namespace Stackline.API.Warehouses;

using global::Stackline.API.Auth;
using global::Stackline.API.Data;
using global::Stackline.API.Warehouses.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Stackline.API.Data.Entities;

public static class WarehouseEndpoints
{
    public static IEndpointRouteBuilder MapWarehouseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/warehouses")
            .WithTags("Warehouses")
            .RequireAuthorization(new AuthorizeAttribute { Roles = $"{Roles.Owner},{Roles.Manager},{Roles.Staff}" });

        group.MapGet("/", async (AppDbContext db, CancellationToken ct) =>
        {
            var warehouses = await db.Warehouses
                .AsNoTracking()
                .OrderBy(w => w.Name)
                .Select(w => new WarehouseResponse(w.Id, w.Name, w.Location, w.IsActive, w.CreatedAt))
                .ToListAsync(ct);

            return Results.Ok(warehouses);
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var warehouse = await db.Warehouses
                .AsNoTracking()
                .Where(w => w.Id == id)
                .Select(w => new WarehouseResponse(w.Id, w.Name, w.Location, w.IsActive, w.CreatedAt))
                .FirstOrDefaultAsync(ct);

            return warehouse is not null ? Results.Ok(warehouse) : Results.NotFound();
        });

        group.MapPost("/", async (CreateWarehouseRequest request, AppDbContext db, CancellationToken ct) =>
        {
            var warehouse = new Warehouse
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Location = request.Location
            };

            db.Warehouses.Add(warehouse);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/warehouses/{warehouse.Id}",
                new WarehouseResponse(warehouse.Id, warehouse.Name, warehouse.Location, warehouse.IsActive, warehouse.CreatedAt));
        })
        .RequireAuthorization(new AuthorizeAttribute { Roles = $"{Roles.Owner},{Roles.Manager}" });

        group.MapPut("/{id:guid}", async (Guid id, UpdateWarehouseRequest request, AppDbContext db, CancellationToken ct) =>
        {
            var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == id, ct);
            if (warehouse is null) return Results.NotFound();

            warehouse.Name = request.Name;
            warehouse.Location = request.Location;
            warehouse.IsActive = request.IsActive;

            await db.SaveChangesAsync(ct);
            return Results.Ok(new WarehouseResponse(warehouse.Id, warehouse.Name, warehouse.Location, warehouse.IsActive, warehouse.CreatedAt));
        })
        .RequireAuthorization(new AuthorizeAttribute { Roles = $"{Roles.Owner},{Roles.Manager}" });

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == id, ct);
            if (warehouse is null) return Results.NotFound();

            warehouse.IsDeleted = true;
            warehouse.IsActive = false;
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .RequireAuthorization(new AuthorizeAttribute { Roles = Roles.Owner });

        return app;
    }
}
