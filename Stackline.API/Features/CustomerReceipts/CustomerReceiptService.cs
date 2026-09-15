using Microsoft.EntityFrameworkCore;
using Stackline.API.Common;
using Stackline.API.Data;
using Stackline.API.Data.Entities;
using Stackline.API.Features.Auth;
using Stackline.API.Features.CustomerReceipts.Dtos;
using Stackline.API.Features.Customers;

namespace Stackline.API.Features.CustomerReceipts;

public sealed class CustomerReceiptService : ICustomerReceiptService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICustomerBalanceService _balanceService;

    private const decimal MaxAmount = 9999999999999999.99m;

    public CustomerReceiptService(
        AppDbContext db,
        ICurrentUserService currentUser,
        ICustomerBalanceService balanceService)
    {
        _db = db;
        _currentUser = currentUser;
        _balanceService = balanceService;
    }

    public async Task<Result<CustomerReceiptResponse>> CreateAsync(
        CreateCustomerReceiptRequest request,
        CancellationToken ct)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(ct);

        var validation = await ValidateAsync(request, ct);

        if (!validation.IsSuccess)
        {
            return Result<CustomerReceiptResponse>.Failure(
                validation.Error);
        }

        var id = Guid.NewGuid();

        var receipt = new CustomerReceipt
        {
            Id = id,
            ReceiptNumber = $"REC-{id:N}",
            Status = CustomerReceiptStatuses.Draft,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        ApplyDraft(receipt, request);

        _db.CustomerReceipts.Add(receipt);

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<CustomerReceiptResponse>.Success(
            ToResponse(receipt));
    }

    public async Task<Result<CustomerReceiptResponse>> UpdateAsync(
        Guid id,
        UpdateCustomerReceiptRequest request,
        CancellationToken ct)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(ct);

        var receipt = await LockReceiptAsync(id, ct);

        if (receipt is null)
        {
            return Result<CustomerReceiptResponse>.Failure(
                CustomerReceiptErrors.NotFound);
        }

        if (receipt.Status != CustomerReceiptStatuses.Draft)
        {
            return Result<CustomerReceiptResponse>.Failure(
                CustomerReceiptErrors.NotDraft);
        }

        var draft = new CreateCustomerReceiptRequest(
            request.CustomerId,
            request.ReceiptDate,
            request.Amount,
            request.PaymentMethod,
            request.PaymentReference,
            request.Notes);

        var validation = await ValidateAsync(draft, ct);

        if (!validation.IsSuccess)
        {
            return Result<CustomerReceiptResponse>.Failure(
                validation.Error);
        }

        ApplyDraft(receipt, draft);

        receipt.UpdatedAt = DateTime.UtcNow;
        receipt.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<CustomerReceiptResponse>.Success(
            ToResponse(receipt));
    }

    public async Task<Result<Guid>> DeleteAsync(
        Guid id,
        CancellationToken ct)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(ct);

        var receipt = await LockReceiptAsync(id, ct);

        if (receipt is null)
        {
            return Result<Guid>.Failure(
                CustomerReceiptErrors.NotFound);
        }

        if (receipt.Status != CustomerReceiptStatuses.Draft)
        {
            return Result<Guid>.Failure(
                CustomerReceiptErrors.NotDraft);
        }

        receipt.IsDeleted = true;
        receipt.IsActive = false;
        receipt.UpdatedAt = DateTime.UtcNow;
        receipt.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<Guid>.Success(id);
    }

    public async Task<Result<CustomerReceiptResponse>> PostAsync(
        Guid id,
        CancellationToken ct)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(ct);

        var receipt = await LockReceiptAsync(id, ct);

        if (receipt is null)
        {
            return Result<CustomerReceiptResponse>.Failure(
                CustomerReceiptErrors.NotFound);
        }

        if (receipt.Status == CustomerReceiptStatuses.Posted)
        {
            return Result<CustomerReceiptResponse>.Failure(
                CustomerReceiptErrors.AlreadyPosted);
        }

        if (receipt.Status != CustomerReceiptStatuses.Draft)
        {
            return Result<CustomerReceiptResponse>.Failure(
                CustomerReceiptErrors.NotDraft);
        }

        var request = new CreateCustomerReceiptRequest(
            receipt.CustomerId,
            receipt.ReceiptDate,
            receipt.Amount,
            receipt.PaymentMethod,
            receipt.PaymentReference,
            receipt.Notes);

        // Revalidates values and locks the customer.
        var validation = await ValidateAsync(request, ct);

        if (!validation.IsSuccess)
        {
            return Result<CustomerReceiptResponse>.Failure(
                validation.Error);
        }

        var balanceResult = await _balanceService.GetAsync(
            receipt.CustomerId,
            ct);

        if (!balanceResult.IsSuccess)
        {
            return Result<CustomerReceiptResponse>.Failure(
                balanceResult.Error);
        }

        // Overpayments are allowed as customer credit.
        // Check numeric range before posting.
        try
        {
            var balance = balanceResult.Value;

            var resultingBalance = checked(
                balance.OutstandingBalance - receipt.Amount);

            _ = checked(balance.PostedReceiptsAmount + receipt.Amount);
            _ = checked(balance.CreditLimit - resultingBalance);
        }
        catch (OverflowException)
        {
            return Result<CustomerReceiptResponse>.Failure(
                CustomerErrors.BalanceOutOfRange);
        }

        var now = DateTime.UtcNow;

        receipt.Status = CustomerReceiptStatuses.Posted;
        receipt.PostedAt = now;
        receipt.PostedBy = _currentUser.UserId;
        receipt.UpdatedAt = now;
        receipt.UpdatedBy = _currentUser.UserId;

        // The balance service includes this amount once it is Posted.
        // OpeningBalance and sale AmountPaid are not changed.
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<CustomerReceiptResponse>.Success(
            ToResponse(receipt));
    }

    private async Task<CustomerReceipt?> LockReceiptAsync(
        Guid id,
        CancellationToken ct)
    {
        var receipts = await _db.CustomerReceipts
            .FromSqlInterpolated($"""
                SELECT *
                FROM "CustomerReceipts"
                WHERE "Id" = {id}
                  AND NOT "IsDeleted"
                FOR UPDATE
                """)
            .IgnoreQueryFilters()
            .ToListAsync(ct);

        return receipts.SingleOrDefault();
    }

    private async Task<Result<bool>> ValidateAsync(
        CreateCustomerReceiptRequest request,
        CancellationToken ct)
    {
        if (request.ReceiptDate == default)
        {
            return Result<bool>.Failure(
                CustomerReceiptErrors.InvalidDate);
        }

        if (request.Amount <= 0 ||
            decimal.Round(request.Amount, 2) != request.Amount)
        {
            return Result<bool>.Failure(
                CustomerReceiptErrors.InvalidAmount);
        }

        if (request.Amount > MaxAmount)
        {
            return Result<bool>.Failure(
                CustomerReceiptErrors.AmountTooLarge);
        }

        if (!ReceiptPaymentMethods.IsValid(request.PaymentMethod))
        {
            return Result<bool>.Failure(
                CustomerReceiptErrors.InvalidPaymentMethod);
        }

        if ((request.PaymentReference?.Trim().Length ?? 0) > 100 ||
            (request.Notes?.Trim().Length ?? 0) > 2000)
        {
            return Result<bool>.Failure(
                CustomerReceiptErrors.InvalidDetails);
        }

        // Inactive customers can still pay their outstanding balance.
        var customers = await _db.Customers
            .FromSqlInterpolated($"""
                SELECT *
                FROM "Customers"
                WHERE "Id" = {request.CustomerId}
                  AND NOT "IsDeleted"
                FOR UPDATE
                """)
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(ct);

        if (customers.Count == 0)
        {
            return Result<bool>.Failure(
                CustomerReceiptErrors.CustomerUnavailable);
        }

        return Result<bool>.Success(true);
    }

    private static void ApplyDraft(
        CustomerReceipt receipt,
        CreateCustomerReceiptRequest request)
    {
        receipt.CustomerId = request.CustomerId;
        receipt.ReceiptDate = request.ReceiptDate;
        receipt.Amount = request.Amount;
        receipt.PaymentMethod = request.PaymentMethod;
        receipt.PaymentReference = TrimOrNull(request.PaymentReference);
        receipt.Notes = TrimOrNull(request.Notes);
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static CustomerReceiptResponse ToResponse(
        CustomerReceipt receipt) => new(
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
            receipt.CreatedAt);
}