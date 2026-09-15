using Microsoft.EntityFrameworkCore;
using Stackline.API.Common;
using Stackline.API.Data;
using Stackline.API.Features.Purchases;
using Stackline.API.Features.SupplierPayments;
using Stackline.API.Features.Suppliers.Dtos;

namespace Stackline.API.Features.Suppliers;

public sealed class SupplierBalanceService : ISupplierBalanceService
{
    private readonly AppDbContext _db;

    public SupplierBalanceService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<SupplierBalanceResponse>> GetAsync(
        Guid supplierId,
        CancellationToken ct)
    {
        try
        {
            // Read the supplier and both totals in one query.
            var data = await _db.Suppliers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(supplier =>
                    supplier.Id == supplierId &&
                    !supplier.IsDeleted)
                .Select(supplier => new
                {
                    supplier.Id,
                    supplier.Name,
                    supplier.OpeningBalance,

                    Purchases = _db.Purchases
                        .IgnoreQueryFilters()
                        .Where(purchase =>
                            purchase.SupplierId == supplier.Id &&
                            purchase.Status == PurchaseStatuses.Posted)
                        .Sum(purchase => (decimal?)purchase.TotalAmount)
                        ?? 0m,

                    Payments = _db.SupplierPayments
                        .IgnoreQueryFilters()
                        .Where(payment =>
                            payment.SupplierId == supplier.Id &&
                            payment.Status == SupplierPaymentStatuses.Posted)
                        .Sum(payment => (decimal?)payment.Amount)
                        ?? 0m
                })
                .SingleOrDefaultAsync(ct);

            if (data is null)
            {
                return Result<SupplierBalanceResponse>.Failure(
                    SupplierErrors.NotFound);
            }

            var outstanding = checked(
                data.OpeningBalance +
                data.Purchases -
                data.Payments);

            return Result<SupplierBalanceResponse>.Success(
                new SupplierBalanceResponse(
                    data.Id,
                    data.Name,
                    data.OpeningBalance,
                    data.Purchases,
                    data.Payments,
                    outstanding));
        }
        catch (OverflowException)
        {
            return Result<SupplierBalanceResponse>.Failure(
                SupplierErrors.BalanceOutOfRange);
        }
    }
}