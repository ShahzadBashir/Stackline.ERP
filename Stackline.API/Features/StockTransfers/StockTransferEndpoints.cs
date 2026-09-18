using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Stackline.API.Common;
using Stackline.API.Data;
using Stackline.API.Features.Auth;
using Stackline.API.Features.StockTransfers.Dtos;

namespace Stackline.API.Features.StockTransfers;

public static class StockTransferEndpoints
{
    public static IEndpointRouteBuilder MapStockTransferEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stock-transfers")
            .WithTags("Stock Transfers")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Roles = $"{Roles.Owner},{Roles.Manager},{Roles.Staff}"
            });

        group.MapGet("/", async (
            Guid? warehouseId,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var query = db.StockTransfers.AsNoTracking();

            if (warehouseId.HasValue)
            {
                query = query.Where(transfer =>
                    transfer.SourceWarehouseId == warehouseId.Value ||
                    transfer.DestinationWarehouseId == warehouseId.Value);
            }

            var transfers = await query
                .OrderByDescending(transfer => transfer.TransferDate)
                .ThenByDescending(transfer => transfer.CreatedAt)
                .ThenBy(transfer => transfer.Id)
                .Select(transfer => new StockTransferSummaryResponse(
                    transfer.Id,
                    transfer.TransferNumber,
                    transfer.SourceWarehouseId,
                    transfer.DestinationWarehouseId,
                    transfer.TransferDate,
                    transfer.Status,
                    transfer.Lines.Count,
                    transfer.CreatedAt))
                .ToListAsync(ct);

            return Results.Ok(transfers);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var transfer = await db.StockTransfers
                .AsNoTracking()
                .Include(t => t.Lines)
                .AsSingleQuery()
                .FirstOrDefaultAsync(t => t.Id == id, ct);

            var result = transfer is null
                ? Result<StockTransferResponse>.Failure(
                    StockTransferErrors.NotFound)
                : Result<StockTransferResponse>.Success(
                    StockTransferService.ToResponse(transfer));

            return result.ToHttpResult(value => Results.Ok(value));
        });

        group.MapPost("/", async (
            SaveStockTransferRequest request,
            IStockTransferService service,
            CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);

            return result.ToHttpResult(value =>
                Results.Created(
                    $"/api/stock-transfers/{value.Id}",
                    value));
        })
        .RequireAuthorization(new AuthorizeAttribute
        {
            Roles = $"{Roles.Owner},{Roles.Manager}"
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            SaveStockTransferRequest request,
            IStockTransferService service,
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
            IStockTransferService service,
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
            IStockTransferService service,
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