using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Stackline.API.Common;
using Stackline.API.Data;
using Stackline.API.Features.Auth;
using Stackline.API.Features.CustomerReceipts.Dtos;

namespace Stackline.API.Features.CustomerReceipts;

public static class CustomerReceiptEndpoints
{
    public static IEndpointRouteBuilder MapCustomerReceiptEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customer-receipts")
            .WithTags("Customer Receipts")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Roles = $"{Roles.Owner},{Roles.Manager},{Roles.Staff}"
            });

        group.MapGet("/", async (
            Guid? customerId,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var query = db.CustomerReceipts.AsNoTracking();

            if (customerId.HasValue)
            {
                query = query.Where(
                    receipt => receipt.CustomerId == customerId.Value);
            }

            var receipts = await query
                .OrderByDescending(receipt => receipt.ReceiptDate)
                .ThenByDescending(receipt => receipt.CreatedAt)
                .ThenBy(receipt => receipt.Id)
                .Select(receipt => new CustomerReceiptResponse(
                    receipt.Id,
                    receipt.ReceiptNumber,
                    receipt.CustomerId,
                    receipt.ReceiptDate,
                    receipt.Amount,
                    receipt.PaymentMethod,
                    receipt.PaymentReference,
                    receipt.Notes,
                    receipt.Status,
                    receipt.PostedAt,
                    receipt.CreatedAt))
                .ToListAsync(ct);

            return Results.Ok(receipts);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var receipt = await db.CustomerReceipts
                .AsNoTracking()
                .FirstOrDefaultAsync(receipt => receipt.Id == id, ct);

            var result = receipt is null
                ? Result<CustomerReceiptResponse>.Failure(
                    CustomerReceiptErrors.NotFound)
                : Result<CustomerReceiptResponse>.Success(
                    CustomerReceiptService.ToResponse(receipt));

            return result.ToHttpResult(value => Results.Ok(value));
        });

        group.MapPost("/", async (
            CreateCustomerReceiptRequest request,
            ICustomerReceiptService service,
            CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);

            return result.ToHttpResult(value =>
                Results.Created(
                    $"/api/customer-receipts/{value.Id}",
                    value));
        })
        .RequireAuthorization(new AuthorizeAttribute
        {
            Roles = $"{Roles.Owner},{Roles.Manager}"
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateCustomerReceiptRequest request,
            ICustomerReceiptService service,
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
            ICustomerReceiptService service,
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
            ICustomerReceiptService service,
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