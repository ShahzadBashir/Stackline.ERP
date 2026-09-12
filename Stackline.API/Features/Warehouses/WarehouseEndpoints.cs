namespace Stackline.API.Features.Warehouses;

using Stackline.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Stackline.API.Common;
using Stackline.API.Data.Entities;
using Stackline.API.Features.Warehouses.Dtos;
using Stackline.API.Features.Auth;

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

        group.MapDelete("/{id:guid}", async (
            Guid id,
            AppDbContext db,
            CancellationToken ct) =>
        {

            await using var transaction =
            await db.Database.BeginTransactionAsync(ct);

            var warehouses = await db.Warehouses
                .FromSqlInterpolated($"""
                SELECT *
                FROM "Warehouses"
                WHERE "Id" = {id}
                    AND NOT "IsDeleted"
                FOR UPDATE
                """)
                .IgnoreQueryFilters()
                .ToListAsync(ct);

            var warehouse = warehouses.SingleOrDefault();

            if (warehouse is null)
            {
                return Result<Guid>
                    .Failure(WarehouseErrors.NotFound)
                    .ToHttpResult(_ => Results.NoContent());
            }

            var hasStock = await db.StockLevels
                .AnyAsync(
                    s => s.WarehouseId == id && s.QuantityOnHand != 0,
                    ct);

            if (hasStock)
            {
                return Result<Guid>
                    .Failure(WarehouseErrors.HasStock)
                    .ToHttpResult(_ => Results.NoContent());
            }

            warehouse.IsDeleted = true;
            warehouse.IsActive = false;
            warehouse.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);

            return Result<Guid>
                .Success(id)
                .ToHttpResult(_ => Results.NoContent());
        })
        .RequireAuthorization(new AuthorizeAttribute
        {
            Roles = Roles.Owner
        });

        return app;
    }
}
