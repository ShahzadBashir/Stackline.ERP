using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Stackline.API.Common;
using Stackline.API.Data;
using Stackline.API.Data.Entities;
using Stackline.API.Features.Auth;
using Stackline.API.Features.Purchases;
using Stackline.API.Features.Statements;
using Stackline.API.Features.SupplierPayments;
using Stackline.API.Features.Suppliers.Dtos;

namespace Stackline.API.Features.Suppliers;

public static class SupplierEndpoints
{
    public static IEndpointRouteBuilder MapSupplierEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/suppliers")
            .WithTags("Suppliers")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Roles = $"{Roles.Owner},{Roles.Manager},{Roles.Staff}"
            });

        group.MapGet("/", async (
            AppDbContext db,
            CancellationToken ct) =>
        {
            var suppliers = await db.Suppliers
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .ThenBy(s => s.Id)
                .Select(s => new SupplierResponse(
                    s.Id,
                    s.Name,
                    s.ContactPerson,
                    s.Phone,
                    s.OpeningBalance,
                    s.IsActive,
                    s.CreatedAt))
                .ToListAsync(ct);

            return Results.Ok(suppliers);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var supplier = await db.Suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id, ct);

            var result = supplier is null
                ? Result<SupplierResponse>.Failure(SupplierErrors.NotFound)
                : Result<SupplierResponse>.Success(ToResponse(supplier));

            return result.ToHttpResult(value => Results.Ok(value));
        });

        group.MapPost("/", async (
            CreateSupplierRequest request,
            AppDbContext db,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Result<SupplierResponse>
                    .Failure(SupplierErrors.NameRequired)
                    .ToHttpResult(value => Results.Ok(value));
            }

            var supplier = new Supplier
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                ContactPerson = TrimOrNull(request.ContactPerson),
                Phone = TrimOrNull(request.Phone),
                OpeningBalance = request.OpeningBalance
            };

            db.Suppliers.Add(supplier);
            await db.SaveChangesAsync(ct);

            return Result<SupplierResponse>
                .Success(ToResponse(supplier))
                .ToHttpResult(value =>
                    Results.Created($"/api/suppliers/{value.Id}", value));
        })
        .RequireAuthorization(new AuthorizeAttribute
        {
            Roles = $"{Roles.Owner},{Roles.Manager}"
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateSupplierRequest request,
            AppDbContext db,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            await using var transaction =
                await db.Database.BeginTransactionAsync(ct);

            var supplier = await LockSupplierAsync(id, db, ct);

            if (supplier is null)
            {
                return Result<SupplierResponse>
                    .Failure(SupplierErrors.NotFound)
                    .ToHttpResult(value => Results.Ok(value));
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Result<SupplierResponse>
                    .Failure(SupplierErrors.NameRequired)
                    .ToHttpResult(value => Results.Ok(value));
            }

            if (request.OpeningBalance != supplier.OpeningBalance)
            {
                var hasPostedPurchases = await db.Purchases
                    .IgnoreQueryFilters()
                    .AnyAsync(
                        purchase => purchase.SupplierId == id &&
                                    purchase.Status == PurchaseStatuses.Posted,
                        ct);

                var hasPostedPayments = await db.SupplierPayments
                    .IgnoreQueryFilters()
                    .AnyAsync(
                        payment => payment.SupplierId == id &&
                                   payment.Status == SupplierPaymentStatuses.Posted,
                        ct);

                if (hasPostedPurchases || hasPostedPayments)
                {
                    return Result<SupplierResponse>
                        .Failure(SupplierErrors.OpeningBalanceLocked)
                        .ToHttpResult(value => Results.Ok(value));
                }
            }

            supplier.Name = request.Name.Trim();
            supplier.ContactPerson = TrimOrNull(request.ContactPerson);
            supplier.Phone = TrimOrNull(request.Phone);
            supplier.OpeningBalance = request.OpeningBalance;
            supplier.IsActive = request.IsActive;
            supplier.UpdatedAt = DateTime.UtcNow;
            supplier.UpdatedBy = currentUser.UserId;

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return Result<SupplierResponse>
                .Success(ToResponse(supplier))
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

            var supplier = await LockSupplierAsync(id, db, ct);

            if (supplier is null)
            {
                return Result<Guid>
                    .Failure(SupplierErrors.NotFound)
                    .ToHttpResult(_ => Results.NoContent());
            }

            var hasPurchases = await db.Purchases
                .IgnoreQueryFilters()
                .AnyAsync(
                    purchase => purchase.SupplierId == id &&
                        (!purchase.IsDeleted ||
                         purchase.Status == PurchaseStatuses.Posted),
                    ct);

            var hasPayments = await db.SupplierPayments
                .IgnoreQueryFilters()
                .AnyAsync(
                    payment => payment.SupplierId == id &&
                        (!payment.IsDeleted ||
                         payment.Status == SupplierPaymentStatuses.Posted),
                    ct);

            if (hasPurchases || hasPayments || supplier.OpeningBalance != 0)
            {
                return Result<Guid>
                    .Failure(SupplierErrors.InUse)
                    .ToHttpResult(_ => Results.NoContent());
            }

            supplier.IsDeleted = true;
            supplier.IsActive = false;
            supplier.UpdatedAt = DateTime.UtcNow;
            supplier.UpdatedBy = currentUser.UserId;

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

        group.MapGet("/{id:guid}/balance", async (
            Guid id,
            ISupplierBalanceService balanceService,
            CancellationToken ct) =>
        {
            var result = await balanceService.GetAsync(id, ct);

            return result.ToHttpResult(value => Results.Ok(value));
        });

        group.MapGet("/{id:guid}/statement", async (
            Guid id,
            DateOnly fromDate,
            DateOnly toDate,
            int? page,
            int? pageSize,
            IAccountStatementService statementService,
            CancellationToken ct) =>
        {
            var result = await statementService.GetSupplierAsync(
                id,
                fromDate,
                toDate,
                page ?? 1,
                pageSize ?? 20,
                ct);

            return result.ToHttpResult(value => Results.Ok(value));
        });

        return app;
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static SupplierResponse ToResponse(Supplier supplier) => new(
        supplier.Id,
        supplier.Name,
        supplier.ContactPerson,
        supplier.Phone,
        supplier.OpeningBalance,
        supplier.IsActive,
        supplier.CreatedAt);

    private static async Task<Supplier?> LockSupplierAsync(
    Guid id,
    AppDbContext db,
    CancellationToken ct)
    {
        var suppliers = await db.Suppliers
            .FromSqlInterpolated($"""
            SELECT *
            FROM "Suppliers"
            WHERE "Id" = {id}
              AND NOT "IsDeleted"
            FOR UPDATE
            """)
            .IgnoreQueryFilters()
            .ToListAsync(ct);

        return suppliers.SingleOrDefault();
    }
}