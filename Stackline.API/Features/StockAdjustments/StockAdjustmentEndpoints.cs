using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Stackline.API.Common;
using Stackline.API.Data;
using Stackline.API.Features.Auth;
using Stackline.API.Features.StockAdjustments.Dtos;

namespace Stackline.API.Features.StockAdjustments;

public static class StockAdjustmentEndpoints
{
    public static IEndpointRouteBuilder MapStockAdjustmentEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stock-adjustments")
            .WithTags("Stock Adjustments")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Roles = $"{Roles.Owner},{Roles.Manager},{Roles.Staff}"
            });

        group.MapGet("/", async (
            Guid? warehouseId,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var query = db.StockAdjustments.AsNoTracking();

            if (warehouseId.HasValue)
            {
                query = query.Where(adjustment =>
                    adjustment.WarehouseId == warehouseId.Value);
            }

            var adjustments = await query
                .OrderByDescending(adjustment => adjustment.AdjustmentDate)
                .ThenByDescending(adjustment => adjustment.CreatedAt)
                .ThenBy(adjustment => adjustment.Id)
                .Select(adjustment => new StockAdjustmentSummaryResponse(
                    adjustment.Id,
                    adjustment.AdjustmentNumber,
                    adjustment.WarehouseId,
                    adjustment.AdjustmentDate,
                    adjustment.Reason,
                    adjustment.Status,
                    adjustment.Lines.Count,
                    adjustment.CreatedAt))
                .ToListAsync(ct);

            return Results.Ok(adjustments);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var adjustment = await db.StockAdjustments
                .AsNoTracking()
                .Include(a => a.Lines)
                .AsSingleQuery()
                .FirstOrDefaultAsync(a => a.Id == id, ct);

            var result = adjustment is null
                ? Result<StockAdjustmentResponse>.Failure(
                    StockAdjustmentErrors.NotFound)
                : Result<StockAdjustmentResponse>.Success(
                    StockAdjustmentService.ToResponse(adjustment));

            return result.ToHttpResult(value => Results.Ok(value));
        });

        group.MapPost("/", async (
            SaveStockAdjustmentRequest request,
            IStockAdjustmentService service,
            CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);

            return result.ToHttpResult(value =>
                Results.Created(
                    $"/api/stock-adjustments/{value.Id}",
                    value));
        })
        .RequireAuthorization(new AuthorizeAttribute
        {
            Roles = $"{Roles.Owner},{Roles.Manager}"
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            SaveStockAdjustmentRequest request,
            IStockAdjustmentService service,
            CancellationToken ct) =>
        {
            var result = await service.UpdateAsync(id, request, ct);

            return result.ToHttpResult(value => Results.Ok(value));
        })
        .RequireAuthorization(new AuthorizeAttribute
        {
            Roles = $"{Roles.Owner},{Roles.Manager}"
        });

        group.MapPost("/{id:guid}/post", async (
            Guid id,
            IStockAdjustmentService service,
            CancellationToken ct) =>
        {
            var result = await service.PostAsync(id, ct);

            return result.ToHttpResult(value => Results.Ok(value));
        })
        .RequireAuthorization(new AuthorizeAttribute
        {
            Roles = $"{Roles.Owner},{Roles.Manager}"
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            IStockAdjustmentService service,
            CancellationToken ct) =>
        {
            var result = await service.DeleteAsync(id, ct);

            return result.ToHttpResult(_ => Results.NoContent());
        })
        .RequireAuthorization(new AuthorizeAttribute
        {
            Roles = Roles.Owner
        });

        return app;
    }
}