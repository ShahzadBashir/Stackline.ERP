using Microsoft.EntityFrameworkCore;
using Stackline.API.Common;
using Stackline.API.Data;
using Stackline.API.Features.CustomerReceipts;
using Stackline.API.Features.Customers.Dtos;
using Stackline.API.Features.Sales;

namespace Stackline.API.Features.Customers;

public sealed class CustomerBalanceService : ICustomerBalanceService
{
    private readonly AppDbContext _db;

    public CustomerBalanceService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<CustomerBalanceResponse>> GetAsync(
        Guid customerId,
        CancellationToken ct)
    {
        try
        {
            var data = await _db.Customers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(customer =>
                    customer.Id == customerId &&
                    !customer.IsDeleted)
                .Select(customer => new
                {
                    customer.Id,
                    customer.Name,
                    customer.OpeningBalance,
                    customer.CreditLimit,

                    SalesUnpaid = _db.Sales
                        .IgnoreQueryFilters()
                        .Where(sale =>
                            sale.CustomerId == customer.Id &&
                            sale.Status == SaleStatuses.Posted)
                        .Sum(sale =>
                            (decimal?)(sale.TotalAmount - sale.AmountPaid))
                        ?? 0m,

                    Receipts = _db.CustomerReceipts
                        .IgnoreQueryFilters()
                        .Where(receipt =>
                            receipt.CustomerId == customer.Id &&
                            receipt.Status == CustomerReceiptStatuses.Posted)
                        .Sum(receipt => (decimal?)receipt.Amount)
                        ?? 0m
                })
                .SingleOrDefaultAsync(ct);

            if (data is null)
            {
                return Result<CustomerBalanceResponse>.Failure(
                    CustomerErrors.NotFound);
            }

            var outstanding = checked(
                data.OpeningBalance +
                data.SalesUnpaid -
                data.Receipts);

            var availableCredit = Math.Max(
                0m,
                checked(data.CreditLimit - outstanding));

            return Result<CustomerBalanceResponse>.Success(
                new CustomerBalanceResponse(
                    data.Id,
                    data.Name,
                    data.OpeningBalance,
                    data.SalesUnpaid,
                    data.Receipts,
                    outstanding,
                    data.CreditLimit,
                    availableCredit));
        }
        catch (OverflowException)
        {
            return Result<CustomerBalanceResponse>.Failure(
                CustomerErrors.BalanceOutOfRange);
        }
    }
}