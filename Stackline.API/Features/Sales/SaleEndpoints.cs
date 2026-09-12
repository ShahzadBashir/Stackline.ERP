using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Stackline.API.Common;
using Stackline.API.Data;
using Stackline.API.Features.Auth;
using Stackline.API.Features.Sales.Dtos;

namespace Stackline.API.Features.Sales;

public static class SaleEndpoints
{
    public static IEndpointRouteBuilder MapSaleEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sales")
            .WithTags("Sales")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Roles = $"{Roles.Owner},{Roles.Manager},{Roles.Staff}"
            });

        group.MapGet("/", async (
            AppDbContext db,
            CancellationToken ct) =>
        {
            var sales = await db.Sales
                .AsNoTracking()
                .OrderByDescending(s => s.SaleDate)
                .ThenByDescending(s => s.CreatedAt)
                .ThenBy(s => s.Id)
                .Select(s => new SaleSummaryResponse(
                    s.Id,
                    s.SaleNumber,
                    s.CustomerId,
                    s.WarehouseId,
                    s.SaleDate,
                    s.Status,
                    s.TotalAmount,
                    s.AmountPaid,
                    s.TotalAmount - s.AmountPaid,
                    s.CreatedAt))
                .ToListAsync(ct);

            return Results.Ok(sales);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var sale = await db.Sales
                .AsNoTracking()
                .Include(s => s.Lines)
                .AsSingleQuery()
                .FirstOrDefaultAsync(s => s.Id == id, ct);

            var result = sale is null
                ? Result<SaleResponse>.Failure(SaleErrors.NotFound)
                : Result<SaleResponse>.Success(
                    SaleService.ToResponse(sale));

            return result.ToHttpResult(value => Results.Ok(value));
        });

        group.MapPost("/", async (
            CreateSaleRequest request,
            ISaleService service,
            CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);

            return result.ToHttpResult(value =>
                Results.Created($"/api/sales/{value.Id}", value));
        })
        .RequireAuthorization(new AuthorizeAttribute
        {
            Roles = $"{Roles.Owner},{Roles.Manager}"
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateSaleRequest request,
            ISaleService service,
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
            ISaleService service,
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
            ISaleService service,
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