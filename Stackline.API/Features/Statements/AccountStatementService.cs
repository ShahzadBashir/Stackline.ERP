using System.Data;
using Microsoft.EntityFrameworkCore;
using Stackline.API.Common;
using Stackline.API.Data;
using Stackline.API.Features.Customers;
using Stackline.API.Features.Statements.Dtos;
using Stackline.API.Features.Suppliers;

namespace Stackline.API.Features.Statements;

public sealed class AccountStatementService : IAccountStatementService
{
    private readonly AppDbContext _db;

    public AccountStatementService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<AccountStatementResponse>> GetCustomerAsync(
        Guid customerId,
        DateOnly fromDate,
        DateOnly toDate,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var error = Validate(fromDate, toDate, page, pageSize);

        if (error is not null)
        {
            return Result<AccountStatementResponse>.Failure(error);
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.RepeatableRead,
                ct);

        var customer = await _db.Customers
            .AsNoTracking()
            .Where(customer => customer.Id == customerId)
            .Select(customer => new
            {
                customer.Id,
                customer.Name,
                customer.OpeningBalance
            })
            .SingleOrDefaultAsync(ct);

        if (customer is null)
        {
            return Result<AccountStatementResponse>.Failure(
                CustomerErrors.NotFound);
        }

        // A sale contributes its full total as an increase and
        // its payment at posting as a decrease.
        //
        // Later customer receipts are separate decreases.
        // Parameters are passed safely by SqlQuery.
        var transactions = _db.Database.SqlQuery<StatementTransaction>($"""
            SELECT
                "SaleDate" AS "TransactionDate",
                COALESCE("PostedAt", "CreatedAt") AS "PostedAt",
                'Sale'::text AS "ReferenceType",
                "Id" AS "ReferenceId",
                "SaleNumber"::text AS "ReferenceNumber",
                'Sale / payment at posting'::text AS "Description",
                "TotalAmount" AS "Increase",
                "AmountPaid" AS "Decrease"
            FROM "Sales"
            WHERE "CustomerId" = {customerId}
              AND "Status" = 'Posted'

            UNION ALL

            SELECT
                "ReceiptDate" AS "TransactionDate",
                COALESCE("PostedAt", "CreatedAt") AS "PostedAt",
                'CustomerReceipt'::text AS "ReferenceType",
                "Id" AS "ReferenceId",
                "ReceiptNumber"::text AS "ReferenceNumber",
                'Customer receipt'::text AS "Description",
                0::numeric AS "Increase",
                "Amount" AS "Decrease"
            FROM "CustomerReceipts"
            WHERE "CustomerId" = {customerId}
              AND "Status" = 'Posted'
            """);

        var result = await BuildAsync(
            customer.Id,
            customer.Name,
            "Customer",
            customer.OpeningBalance,
            transactions,
            fromDate,
            toDate,
            page,
            pageSize,
            ct);

        await transaction.CommitAsync(ct);

        return result;
    }

    public async Task<Result<AccountStatementResponse>> GetSupplierAsync(
        Guid supplierId,
        DateOnly fromDate,
        DateOnly toDate,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var error = Validate(fromDate, toDate, page, pageSize);

        if (error is not null)
        {
            return Result<AccountStatementResponse>.Failure(error);
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.RepeatableRead,
                ct);

        var supplier = await _db.Suppliers
            .AsNoTracking()
            .Where(supplier => supplier.Id == supplierId)
            .Select(supplier => new
            {
                supplier.Id,
                supplier.Name,
                supplier.OpeningBalance
            })
            .SingleOrDefaultAsync(ct);

        if (supplier is null)
        {
            return Result<AccountStatementResponse>.Failure(
                SupplierErrors.NotFound);
        }

        var transactions = _db.Database.SqlQuery<StatementTransaction>($"""
            SELECT
                "PurchaseDate" AS "TransactionDate",
                COALESCE("PostedAt", "CreatedAt") AS "PostedAt",
                'Purchase'::text AS "ReferenceType",
                "Id" AS "ReferenceId",
                "PurchaseNumber"::text AS "ReferenceNumber",
                'Purchase'::text AS "Description",
                "TotalAmount" AS "Increase",
                0::numeric AS "Decrease"
            FROM "Purchases"
            WHERE "SupplierId" = {supplierId}
              AND "Status" = 'Posted'

            UNION ALL

            SELECT
                "PaymentDate" AS "TransactionDate",
                COALESCE("PostedAt", "CreatedAt") AS "PostedAt",
                'SupplierPayment'::text AS "ReferenceType",
                "Id" AS "ReferenceId",
                "PaymentNumber"::text AS "ReferenceNumber",
                'Supplier payment'::text AS "Description",
                0::numeric AS "Increase",
                "Amount" AS "Decrease"
            FROM "SupplierPayments"
            WHERE "SupplierId" = {supplierId}
              AND "Status" = 'Posted'
            """);

        var result = await BuildAsync(
            supplier.Id,
            supplier.Name,
            "Supplier",
            supplier.OpeningBalance,
            transactions,
            fromDate,
            toDate,
            page,
            pageSize,
            ct);

        await transaction.CommitAsync(ct);

        return result;
    }

    private static async Task<Result<AccountStatementResponse>> BuildAsync(
        Guid accountId,
        string accountName,
        string accountType,
        decimal originalOpeningBalance,
        IQueryable<StatementTransaction> transactions,
        DateOnly fromDate,
        DateOnly toDate,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        try
        {
            var previousMovement = await transactions
                .Where(entry => entry.TransactionDate < fromDate)
                .SumAsync(
                    entry => (decimal?)(entry.Increase - entry.Decrease),
                    ct) ?? 0m;

            var openingBalance = checked(
                originalOpeningBalance + previousMovement);

            var period = transactions.Where(entry =>
                entry.TransactionDate >= fromDate &&
                entry.TransactionDate <= toDate);

            var totalCount = await period.CountAsync(ct);

            var totalIncrease = await period
                .SumAsync(entry => (decimal?)entry.Increase, ct)
                ?? 0m;

            var totalDecrease = await period
                .SumAsync(entry => (decimal?)entry.Decrease, ct)
                ?? 0m;

            var closingBalance = checked(
                openingBalance + totalIncrease - totalDecrease);

            // Stable ordering is also used when calculating
            // the balance carried forward from earlier pages.
            var ordered = period
                .OrderBy(entry => entry.TransactionDate)
                .ThenBy(entry => entry.PostedAt)
                .ThenBy(entry => entry.ReferenceType)
                .ThenBy(entry => entry.ReferenceId);

            var skip = (page - 1) * pageSize;

            var previousPagesMovement = skip == 0
                ? 0m
                : await ordered
                    .Take(skip)
                    .SumAsync(
                        entry => (decimal?)(entry.Increase - entry.Decrease),
                        ct) ?? 0m;

            var rows = await ordered
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(ct);

            var runningBalance = checked(
                openingBalance + previousPagesMovement);

            var entries = new List<StatementEntryResponse>();

            foreach (var row in rows)
            {
                runningBalance = checked(
                    runningBalance + row.Increase - row.Decrease);

                entries.Add(new StatementEntryResponse(
                    row.TransactionDate,
                    row.PostedAt,
                    row.ReferenceType,
                    row.ReferenceId,
                    row.ReferenceNumber,
                    row.Description,
                    row.Increase,
                    row.Decrease,
                    runningBalance));
            }

            return Result<AccountStatementResponse>.Success(
                new AccountStatementResponse(
                    accountId,
                    accountName,
                    accountType,
                    fromDate,
                    toDate,
                    openingBalance,
                    totalIncrease,
                    totalDecrease,
                    closingBalance,
                    totalCount,
                    page,
                    pageSize,
                    entries));
        }
        catch (OverflowException)
        {
            return Result<AccountStatementResponse>.Failure(
                StatementErrors.BalanceOutOfRange);
        }
    }

    private static Error? Validate(
        DateOnly fromDate,
        DateOnly toDate,
        int page,
        int pageSize)
    {
        if (fromDate == default ||
            toDate == default ||
            fromDate > toDate)
        {
            return StatementErrors.InvalidDateRange;
        }

        if (page < 1 ||
            pageSize < 1 ||
            pageSize > 100 ||
            (long)(page - 1) * pageSize > int.MaxValue)
        {
            return StatementErrors.InvalidPagination;
        }

        return null;
    }
}