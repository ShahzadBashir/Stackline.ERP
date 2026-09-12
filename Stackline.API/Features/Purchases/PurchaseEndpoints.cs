using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Stackline.API.Common;
using Stackline.API.Data;
using Stackline.API.Features.Auth;
using Stackline.API.Purchases.Dtos;

namespace Stackline.API.Features.Purchases;

public static class PurchaseEndpoints
{
    public static IEndpointRouteBuilder MapPurchaseEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/purchases")
            .WithTags("Purchases")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Roles = $"{Roles.Owner},{Roles.Manager},{Roles.Staff}"
            });

        group.MapGet("/", async (
            AppDbContext db,
            CancellationToken ct) =>
        {
            var purchases = await db.Purchases
                .AsNoTracking()
                .OrderByDescending(p => p.PurchaseDate)
                .ThenByDescending(p => p.CreatedAt)
                .Select(p => new PurchaseSummaryResponse(
                    p.Id,
                    p.PurchaseNumber,
                    p.SupplierId,
                    p.WarehouseId,
                    p.PurchaseDate,
                    p.Status,
                    p.TotalAmount,
                    p.CreatedAt))
                .ToListAsync(ct);

            return Results.Ok(purchases);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var purchase = await db.Purchases
                .AsNoTracking()
                .Include(p => p.Lines)
                .AsSingleQuery()
                .FirstOrDefaultAsync(p => p.Id == id, ct);

            var result = purchase is null
                ? Result<PurchaseResponse>.Failure(PurchaseErrors.NotFound)
                : Result<PurchaseResponse>.Success(
                    PurchaseService.ToResponse(purchase));

            return result.ToHttpResult(value => Results.Ok(value));
        });

        group.MapPost("/", async (
            CreatePurchaseRequest request,
            IPurchaseService service,
            CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);

            return result.ToHttpResult(value =>
                Results.Created($"/api/purchases/{value.Id}", value));
        })
        .RequireAuthorization(new AuthorizeAttribute
        {
            Roles = $"{Roles.Owner},{Roles.Manager}"
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdatePurchaseRequest request,
            IPurchaseService service,
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
            IPurchaseService service,
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
            IPurchaseService service,
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