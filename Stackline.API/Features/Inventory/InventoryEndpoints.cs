using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Stackline.API.Common;
using Stackline.API.Data;
using Stackline.API.Features.Auth;
using Stackline.API.Features.Inventory.Dtos;

namespace Stackline.API.Features.Inventory;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory")
            .WithTags("Inventory")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Roles = $"{Roles.Owner},{Roles.Manager},{Roles.Staff}"
            });

        group.MapGet("/balances", async (
            Guid? warehouseId,
            Guid? itemId,
            bool? lowStockOnly,
            int? page,
            int? pageSize,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var currentPage = page ?? 1;
            var size = pageSize ?? 20;

            if (!ValidPagination(currentPage, size))
            {
                return Result<InventoryPage<StockBalanceResponse>>
                    .Failure(InventoryErrors.InvalidPagination)
                    .ToHttpResult(value => Results.Ok(value));
            }

            var query =
             from stock in db.StockLevels
                .IgnoreQueryFilters()
                .AsNoTracking()
                where !stock.IsDeleted
                join item in db.Items.IgnoreQueryFilters()
                    on stock.ItemId equals item.Id into itemGroup
                from item in itemGroup.DefaultIfEmpty()
                join warehouse in db.Warehouses.IgnoreQueryFilters()
                    on stock.WarehouseId equals warehouse.Id into warehouseGroup
                from warehouse in warehouseGroup.DefaultIfEmpty()
                select new
                {
                    stock.Id,
                    stock.ItemId,
                    SKU = item == null ? "" : item.SKU,
                    ItemName = item == null ? "Unavailable item" : item.Name,
                    UnitOfMeasure = item == null ? "" : item.UnitOfMeasure,
                    stock.WarehouseId,
                    WarehouseName = warehouse == null
                        ? "Unavailable warehouse"
                        : warehouse.Name,
                    stock.QuantityOnHand,
                    ReorderLevel = item == null ? 0 : item.ReorderLevel,
                    IsLowStock = item != null &&
                        item.ReorderLevel > 0 &&
                        stock.QuantityOnHand <= item.ReorderLevel,
                    stock.LastUpdated
                };

            if (warehouseId.HasValue)
            {
                query = query.Where(
                    stock => stock.WarehouseId == warehouseId.Value);
            }

            if (itemId.HasValue)
            {
                query = query.Where(
                    stock => stock.ItemId == itemId.Value);
            }

            if (lowStockOnly == true)
            {
                query = query.Where(stock => stock.IsLowStock);
            }

            var totalCount = await query.CountAsync(ct);

            var balances = await query
                .OrderBy(stock => stock.ItemName)
                .ThenBy(stock => stock.WarehouseName)
                .ThenBy(stock => stock.Id)
                .Skip((currentPage - 1) * size)
                .Take(size)
                .Select(stock => new StockBalanceResponse(
                    stock.Id,
                    stock.ItemId,
                    stock.SKU,
                    stock.ItemName,
                    stock.UnitOfMeasure,
                    stock.WarehouseId,
                    stock.WarehouseName,
                    stock.QuantityOnHand,
                    stock.ReorderLevel,
                    stock.IsLowStock,
                    stock.LastUpdated))
                .ToListAsync(ct);

            return Results.Ok(
                new InventoryPage<StockBalanceResponse>(
                    balances,
                    totalCount,
                    currentPage,
                    size));
        });

        group.MapGet("/movements", async (
            Guid? warehouseId,
            Guid? itemId,
            int? page,
            int? pageSize,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var currentPage = page ?? 1;
            var size = pageSize ?? 20;

            if (!ValidPagination(currentPage, size))
            {
                return Result<InventoryPage<StockMovementResponse>>
                    .Failure(InventoryErrors.InvalidPagination)
                    .ToHttpResult(value => Results.Ok(value));
            }

            var query =
            from movement in db.StockMovements
                .IgnoreQueryFilters()
                .AsNoTracking()
            where !movement.IsDeleted
            join item in db.Items.IgnoreQueryFilters()
                on movement.ItemId equals item.Id into itemGroup
            from item in itemGroup.DefaultIfEmpty()
            join warehouse in db.Warehouses.IgnoreQueryFilters()
                on movement.WarehouseId equals warehouse.Id into warehouseGroup
            from warehouse in warehouseGroup.DefaultIfEmpty()
            select new
            {
                movement.Id,
                movement.ItemId,
                SKU = item == null ? "" : item.SKU,
                ItemName = item == null ? "Unavailable item" : item.Name,
                movement.WarehouseId,
                WarehouseName = warehouse == null
                    ? "Unavailable warehouse"
                    : warehouse.Name,
                movement.MovementType,
                movement.Quantity,
                movement.ReferenceType,
                movement.ReferenceId,
                movement.CreatedAt
            };

            if (warehouseId.HasValue)
            {
                query = query.Where(
                    movement => movement.WarehouseId == warehouseId.Value);
            }

            if (itemId.HasValue)
            {
                query = query.Where(
                    movement => movement.ItemId == itemId.Value);
            }

            var totalCount = await query.CountAsync(ct);

            var movements = await query
                .OrderByDescending(movement => movement.CreatedAt)
                .ThenByDescending(movement => movement.Id)
                .Skip((currentPage - 1) * size)
                .Take(size)
                .Select(movement => new StockMovementResponse(
                    movement.Id,
                    movement.ItemId,
                    movement.SKU,
                    movement.ItemName,
                    movement.WarehouseId,
                    movement.WarehouseName,
                    movement.MovementType,
                    movement.Quantity,
                    movement.ReferenceType,
                    movement.ReferenceId,
                    movement.CreatedAt))
                .ToListAsync(ct);

            return Results.Ok(
                new InventoryPage<StockMovementResponse>(
                    movements,
                    totalCount,
                    currentPage,
                    size));
        });

        return app;
    }

    private static bool ValidPagination(int page, int pageSize) =>
        page > 0 &&
        pageSize >= 1 &&
        pageSize <= 100 &&
        (long)(page - 1) * pageSize <= int.MaxValue;
}