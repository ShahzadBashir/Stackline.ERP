using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Stackline.API.Common;
using Stackline.API.Data;
using Stackline.API.Features.Auth;
using Stackline.API.Features.SupplierPayments.Dtos;

namespace Stackline.API.Features.SupplierPayments;

public static class SupplierPaymentEndpoints
{
    public static IEndpointRouteBuilder MapSupplierPaymentEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/supplier-payments")
            .WithTags("Supplier Payments")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Roles = $"{Roles.Owner},{Roles.Manager},{Roles.Staff}"
            });

        group.MapGet("/", async (
            Guid? supplierId,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var query = db.SupplierPayments.AsNoTracking();

            if (supplierId.HasValue)
            {
                query = query.Where(
                    payment => payment.SupplierId == supplierId.Value);
            }

            var payments = await query
                .OrderByDescending(payment => payment.PaymentDate)
                .ThenByDescending(payment => payment.CreatedAt)
                .ThenBy(payment => payment.Id)
                .Select(payment => new SupplierPaymentResponse(
                    payment.Id,
                    payment.PaymentNumber,
                    payment.SupplierId,
                    payment.PaymentDate,
                    payment.Amount,
                    payment.PaymentMethod,
                    payment.PaymentReference,
                    payment.Notes,
                    payment.Status,
                    payment.PostedAt,
                    payment.CreatedAt))
                .ToListAsync(ct);

            return Results.Ok(payments);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var payment = await db.SupplierPayments
                .AsNoTracking()
                .FirstOrDefaultAsync(payment => payment.Id == id, ct);

            var result = payment is null
                ? Result<SupplierPaymentResponse>.Failure(
                    SupplierPaymentErrors.NotFound)
                : Result<SupplierPaymentResponse>.Success(
                    SupplierPaymentService.ToResponse(payment));

            return result.ToHttpResult(value => Results.Ok(value));
        });

        group.MapPost("/", async (
            CreateSupplierPaymentRequest request,
            ISupplierPaymentService service,
            CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);

            return result.ToHttpResult(value =>
                Results.Created(
                    $"/api/supplier-payments/{value.Id}",
                    value));
        })
        .RequireAuthorization(new AuthorizeAttribute
        {
            Roles = $"{Roles.Owner},{Roles.Manager}"
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateSupplierPaymentRequest request,
            ISupplierPaymentService service,
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
            ISupplierPaymentService service,
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
            ISupplierPaymentService service,
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