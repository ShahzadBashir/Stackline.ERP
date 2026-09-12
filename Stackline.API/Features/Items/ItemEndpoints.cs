using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stackline.API.Common;
using Stackline.API.Data;
using Stackline.API.Data.Entities;
using Stackline.API.Features.Auth;
using Stackline.API.Features.Items.Dtos;

namespace Stackline.API.Features.Items;

public static class ItemEndpoints
{
    public static IEndpointRouteBuilder MapItemEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/items")
            .WithTags("Items")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Roles = $"{Roles.Owner},{Roles.Manager},{Roles.Staff}"
            });

        group.MapGet("/", async (
            AppDbContext db,
            CancellationToken ct) =>
        {
            var items = await db.Items
                .AsNoTracking()
                .OrderBy(i => i.Name)
                .Select(i => new ItemResponse(
                    i.Id,
                    i.SKU,
                    i.Name,
                    i.CategoryId,
                    i.UnitOfMeasure,
                    i.CostPrice,
                    i.SalePrice,
                    i.ReorderLevel,
                    i.IsActive,
                    i.CreatedAt))
                .ToListAsync(ct);

            return Results.Ok(items);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var item = await db.Items
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id, ct);

            var result = item is null
                ? Result<ItemResponse>.Failure(ItemErrors.NotFound)
                : Result<ItemResponse>.Success(ToResponse(item));

            return result.ToHttpResult(value => Results.Ok(value));
        });

        group.MapPost("/", async (
            CreateItemRequest request,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var validation = await ValidateAsync(request, null, db, ct);

            if (!validation.IsSuccess)
            {
                return validation.ToHttpResult(_ => Results.NoContent());
            }

            var item = new Item { Id = Guid.NewGuid() };
            Apply(item, request);

            db.Items.Add(item);

            var result = await SaveAsync(item, db, ct);

            return result.ToHttpResult(value => Results.Created(
                $"/api/items/{value.Id}", value));
        })
        .RequireAuthorization(new AuthorizeAttribute
        {
            Roles = $"{Roles.Owner},{Roles.Manager}"
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateItemRequest request,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var item = await db.Items
                .FirstOrDefaultAsync(i => i.Id == id, ct);

            if (item is null)
            {
                return Result<ItemResponse>
                    .Failure(ItemErrors.NotFound)
                    .ToHttpResult(value => Results.Ok(value));
            }

            var values = new CreateItemRequest(
                request.SKU,
                request.Name,
                request.CategoryId,
                request.UnitOfMeasure,
                request.CostPrice,
                request.SalePrice,
                request.ReorderLevel);

            var validation = await ValidateAsync(values, id, db, ct);

            if (!validation.IsSuccess)
            {
                return validation.ToHttpResult(_ => Results.NoContent());
            }

            Apply(item, values);
            item.IsActive = request.IsActive;
            item.UpdatedAt = DateTime.UtcNow;

            var result = await SaveAsync(item, db, ct);

            return result.ToHttpResult(value => Results.Ok(value));
        })
        .RequireAuthorization(new AuthorizeAttribute
        {
            Roles = $"{Roles.Owner},{Roles.Manager}"
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            AppDbContext db,
            CancellationToken ct) =>
        {
            await using var transaction =
    await db.Database.BeginTransactionAsync(ct);

            var items = await db.Items
                .FromSqlInterpolated($"""
                SELECT *
                FROM "Items"
                WHERE "Id" = {id}
                  AND NOT "IsDeleted"
                FOR UPDATE
                """)
                .IgnoreQueryFilters()
                .ToListAsync(ct);

            var item = items.SingleOrDefault();

            if (item is null)
            {
                return Result<Guid>
                    .Failure(ItemErrors.NotFound)
                    .ToHttpResult(_ => Results.NoContent());
            }

            if (await db.StockLevels.AnyAsync(
                s => s.ItemId == id && s.QuantityOnHand != 0, ct))
            {
                return Result<Guid>
                    .Failure(ItemErrors.HasStock)
                    .ToHttpResult(_ => Results.NoContent());
            }

            item.IsDeleted = true;
            item.IsActive = false;
            item.UpdatedAt = DateTime.UtcNow;

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

    private static ItemResponse ToResponse(Item item) => new(
        item.Id,
        item.SKU,
        item.Name,
        item.CategoryId,
        item.UnitOfMeasure,
        item.CostPrice,
        item.SalePrice,
        item.ReorderLevel,
        item.IsActive,
        item.CreatedAt);

    private static void Apply(Item item, CreateItemRequest request)
    {
        item.SKU = request.SKU.Trim();
        item.Name = request.Name.Trim();
        item.CategoryId = request.CategoryId;
        item.UnitOfMeasure = request.UnitOfMeasure.Trim();
        item.CostPrice = request.CostPrice;
        item.SalePrice = request.SalePrice;
        item.ReorderLevel = request.ReorderLevel;
    }

    private static async Task<Result<bool>> ValidateAsync(
        CreateItemRequest request,
        Guid? itemId,
        AppDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SKU))
            return Result<bool>.Failure(ItemErrors.SKURequired);

        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<bool>.Failure(ItemErrors.NameRequired);

        if (string.IsNullOrWhiteSpace(request.UnitOfMeasure))
            return Result<bool>.Failure(ItemErrors.UnitOfMeasureRequired);

        if (request.CostPrice < 0)
            return Result<bool>.Failure(ItemErrors.InvalidCostPrice);

        if (request.SalePrice < 0)
            return Result<bool>.Failure(ItemErrors.InvalidSalePrice);

        if (request.ReorderLevel < 0)
            return Result<bool>.Failure(ItemErrors.InvalidReorderLevel);

        if (!await db.Categories.AnyAsync(
            c => c.Id == request.CategoryId, ct))
        {
            return Result<bool>.Failure(ItemErrors.CategoryNotFound);
        }

        var sku = request.SKU.Trim();

        if (await db.Items.IgnoreQueryFilters().AnyAsync(
            i => i.SKU == sku && i.Id != itemId, ct))
        {
            return Result<bool>.Failure(ItemErrors.SKUExists);
        }

        return Result<bool>.Success(true);
    }

    private static async Task<Result<ItemResponse>> SaveAsync(
        Item item,
        AppDbContext db,
        CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);

            return Result<ItemResponse>.Success(ToResponse(item));
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_Items_SKU"
            })
        {
            // Handles simultaneous requests using the same SKU.
            return Result<ItemResponse>.Failure(ItemErrors.SKUExists);
        }
    }
}