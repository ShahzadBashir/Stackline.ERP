using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Stackline.API.Common;
using Stackline.API.Data;
using Stackline.API.Data.Entities;
using Stackline.API.Features.Auth;
using Stackline.API.Features.Customers.Dtos;
using Stackline.API.Features.Sales;

namespace Stackline.API.Features.Customers;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers")
            .WithTags("Customers")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Roles = $"{Roles.Owner},{Roles.Manager},{Roles.Staff}"
            });

        group.MapGet("/", async (
            AppDbContext db,
            CancellationToken ct) =>
        {
            var customers = await db.Customers
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ThenBy(c => c.Id)
                .Select(c => new CustomerResponse(
                    c.Id,
                    c.Name,
                    c.ContactPerson,
                    c.Phone,
                    c.CreditLimit,
                    c.OpeningBalance,
                    c.IsActive,
                    c.CreatedAt))
                .ToListAsync(ct);

            return Results.Ok(customers);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var customer = await db.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, ct);

            var result = customer is null
                ? Result<CustomerResponse>.Failure(CustomerErrors.NotFound)
                : Result<CustomerResponse>.Success(ToResponse(customer));

            return result.ToHttpResult(value => Results.Ok(value));
        });

        group.MapPost("/", async (
            CreateCustomerRequest request,
            AppDbContext db,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var error = Validate(request.Name, request.CreditLimit);

            if (error is not null)
            {
                return Result<CustomerResponse>
                    .Failure(error)
                    .ToHttpResult(value => Results.Ok(value));
            }

            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                ContactPerson = TrimOrNull(request.ContactPerson),
                Phone = TrimOrNull(request.Phone),
                CreditLimit = request.CreditLimit,
                OpeningBalance = request.OpeningBalance,
                CreatedBy = currentUser.UserId
            };

            db.Customers.Add(customer);
            await db.SaveChangesAsync(ct);

            return Result<CustomerResponse>
                .Success(ToResponse(customer))
                .ToHttpResult(value =>
                    Results.Created($"/api/customers/{value.Id}", value));
        })
        .RequireAuthorization(new AuthorizeAttribute
        {
            Roles = $"{Roles.Owner},{Roles.Manager}"
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateCustomerRequest request,
            AppDbContext db,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            await using var transaction =
                await db.Database.BeginTransactionAsync(ct);

            var customer = await LockCustomerAsync(id, db, ct);

            if (customer is null)
            {
                return Result<CustomerResponse>
                    .Failure(CustomerErrors.NotFound)
                    .ToHttpResult(value => Results.Ok(value));
            }

            var error = Validate(request.Name, request.CreditLimit);

            if (error is not null)
            {
                return Result<CustomerResponse>
                    .Failure(error)
                    .ToHttpResult(value => Results.Ok(value));
            }

            if (request.OpeningBalance != customer.OpeningBalance)
            {
                var hasPostedSales = await db.Sales
                    .IgnoreQueryFilters()
                    .AnyAsync(
                        sale => sale.CustomerId == id &&
                                sale.Status == SaleStatuses.Posted,
                        ct);

                if (hasPostedSales)
                {
                    return Result<CustomerResponse>
                        .Failure(CustomerErrors.OpeningBalanceLocked)
                        .ToHttpResult(value => Results.Ok(value));
                }
            }

            customer.Name = request.Name.Trim();
            customer.ContactPerson = TrimOrNull(request.ContactPerson);
            customer.Phone = TrimOrNull(request.Phone);
            customer.CreditLimit = request.CreditLimit;
            customer.OpeningBalance = request.OpeningBalance;
            customer.IsActive = request.IsActive;
            customer.UpdatedAt = DateTime.UtcNow;
            customer.UpdatedBy = currentUser.UserId;

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return Result<CustomerResponse>
                .Success(ToResponse(customer))
                .ToHttpResult(value => Results.Ok(value));
        })
        .RequireAuthorization(new AuthorizeAttribute
        {
            Roles = $"{Roles.Owner},{Roles.Manager}"
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            AppDbContext db,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            await using var transaction =
                await db.Database.BeginTransactionAsync(ct);

            var customer = await LockCustomerAsync(id, db, ct);

            if (customer is null)
            {
                return Result<Guid>
                    .Failure(CustomerErrors.NotFound)
                    .ToHttpResult(_ => Results.NoContent());
            }

            var hasSales = await db.Sales
                .IgnoreQueryFilters()
                .AnyAsync(
                    sale => sale.CustomerId == id &&
                        (!sale.IsDeleted || sale.Status == SaleStatuses.Posted),
                    ct);

            if (hasSales || customer.OpeningBalance != 0)
            {
                return Result<Guid>
                    .Failure(CustomerErrors.InUse)
                    .ToHttpResult(_ => Results.NoContent());
            }

            customer.IsDeleted = true;
            customer.IsActive = false;
            customer.UpdatedAt = DateTime.UtcNow;
            customer.UpdatedBy = currentUser.UserId;

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

    private static Error? Validate(string? name, decimal creditLimit)
    {
        if (string.IsNullOrWhiteSpace(name))
            return CustomerErrors.NameRequired;

        if (creditLimit < 0)
            return CustomerErrors.InvalidCreditLimit;

        return null;
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static CustomerResponse ToResponse(Customer customer) => new(
        customer.Id,
        customer.Name,
        customer.ContactPerson,
        customer.Phone,
        customer.CreditLimit,
        customer.OpeningBalance,
        customer.IsActive,
        customer.CreatedAt);

    private static async Task<Customer?> LockCustomerAsync(
    Guid id,
    AppDbContext db,
    CancellationToken ct)
    {
        var customers = await db.Customers
            .FromSqlInterpolated($"""
            SELECT *
            FROM "Customers"
            WHERE "Id" = {id}
              AND NOT "IsDeleted"
            FOR UPDATE
            """)
            .IgnoreQueryFilters()
            .ToListAsync(ct);

        return customers.SingleOrDefault();
    }
}

