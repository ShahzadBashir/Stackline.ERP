using Microsoft.EntityFrameworkCore;
using Stackline.API.Common;
using Stackline.API.Data;
using Stackline.API.Data.Entities;
using Stackline.API.Features.Auth;
using Stackline.API.Features.SupplierPayments.Dtos;
using Stackline.API.Features.Suppliers;

namespace Stackline.API.Features.SupplierPayments;

public sealed class SupplierPaymentService : ISupplierPaymentService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISupplierBalanceService _balanceService;

    private const decimal MaxAmount = 9999999999999999.99m;

    public SupplierPaymentService(
        AppDbContext db,
        ICurrentUserService currentUser,
        ISupplierBalanceService balanceService)
    {
        _db = db;
        _currentUser = currentUser;
        _balanceService = balanceService;
    }

    public async Task<Result<SupplierPaymentResponse>> CreateAsync(
        CreateSupplierPaymentRequest request,
        CancellationToken ct)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(ct);

        var validation = await ValidateAsync(request, ct);

        if (!validation.IsSuccess)
        {
            return Result<SupplierPaymentResponse>.Failure(
                validation.Error);
        }

        var id = Guid.NewGuid();

        var payment = new SupplierPayment
        {
            Id = id,
            PaymentNumber = $"PAY-{id:N}",
            Status = SupplierPaymentStatuses.Draft,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        ApplyDraft(payment, request);

        _db.SupplierPayments.Add(payment);

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<SupplierPaymentResponse>.Success(
            ToResponse(payment));
    }

    public async Task<Result<SupplierPaymentResponse>> UpdateAsync(
        Guid id,
        UpdateSupplierPaymentRequest request,
        CancellationToken ct)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(ct);

        var payment = await LockPaymentAsync(id, ct);

        if (payment is null)
        {
            return Result<SupplierPaymentResponse>.Failure(
                SupplierPaymentErrors.NotFound);
        }

        if (payment.Status != SupplierPaymentStatuses.Draft)
        {
            return Result<SupplierPaymentResponse>.Failure(
                SupplierPaymentErrors.NotDraft);
        }

        var draft = new CreateSupplierPaymentRequest(
            request.SupplierId,
            request.PaymentDate,
            request.Amount,
            request.PaymentMethod,
            request.PaymentReference,
            request.Notes);

        var validation = await ValidateAsync(draft, ct);

        if (!validation.IsSuccess)
        {
            return Result<SupplierPaymentResponse>.Failure(
                validation.Error);
        }

        ApplyDraft(payment, draft);

        payment.UpdatedAt = DateTime.UtcNow;
        payment.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<SupplierPaymentResponse>.Success(
            ToResponse(payment));
    }

    public async Task<Result<Guid>> DeleteAsync(
        Guid id,
        CancellationToken ct)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(ct);

        var payment = await LockPaymentAsync(id, ct);

        if (payment is null)
        {
            return Result<Guid>.Failure(
                SupplierPaymentErrors.NotFound);
        }

        if (payment.Status != SupplierPaymentStatuses.Draft)
        {
            return Result<Guid>.Failure(
                SupplierPaymentErrors.NotDraft);
        }

        payment.IsDeleted = true;
        payment.IsActive = false;
        payment.UpdatedAt = DateTime.UtcNow;
        payment.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<Guid>.Success(id);
    }

    public async Task<Result<SupplierPaymentResponse>> PostAsync(
        Guid id,
        CancellationToken ct)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(ct);

        var payment = await LockPaymentAsync(id, ct);

        if (payment is null)
        {
            return Result<SupplierPaymentResponse>.Failure(
                SupplierPaymentErrors.NotFound);
        }

        if (payment.Status == SupplierPaymentStatuses.Posted)
        {
            return Result<SupplierPaymentResponse>.Failure(
                SupplierPaymentErrors.AlreadyPosted);
        }

        if (payment.Status != SupplierPaymentStatuses.Draft)
        {
            return Result<SupplierPaymentResponse>.Failure(
                SupplierPaymentErrors.NotDraft);
        }

        var request = new CreateSupplierPaymentRequest(
            payment.SupplierId,
            payment.PaymentDate,
            payment.Amount,
            payment.PaymentMethod,
            payment.PaymentReference,
            payment.Notes);

        // Revalidates values and locks the supplier.
        var validation = await ValidateAsync(request, ct);

        if (!validation.IsSuccess)
        {
            return Result<SupplierPaymentResponse>.Failure(
                validation.Error);
        }

        var balanceResult = await _balanceService.GetAsync(
            payment.SupplierId,
            ct);

        if (!balanceResult.IsSuccess)
        {
            return Result<SupplierPaymentResponse>.Failure(
                balanceResult.Error);
        }

        // Paying more than the current balance is allowed.
        // The excess becomes an advance with the supplier.
        try
        {
            var balance = balanceResult.Value;

            _ = checked(balance.OutstandingBalance - payment.Amount);
            _ = checked(balance.PostedPaymentsAmount + payment.Amount);
        }
        catch (OverflowException)
        {
            return Result<SupplierPaymentResponse>.Failure(
                SupplierErrors.BalanceOutOfRange);
        }

        var now = DateTime.UtcNow;

        payment.Status = SupplierPaymentStatuses.Posted;
        payment.PostedAt = now;
        payment.PostedBy = _currentUser.UserId;
        payment.UpdatedAt = now;
        payment.UpdatedBy = _currentUser.UserId;

        // Do not modify OpeningBalance, purchase totals, or stock.
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<SupplierPaymentResponse>.Success(
            ToResponse(payment));
    }

    private async Task<SupplierPayment?> LockPaymentAsync(
        Guid id,
        CancellationToken ct)
    {
        var payments = await _db.SupplierPayments
            .FromSqlInterpolated($"""
                SELECT *
                FROM "SupplierPayments"
                WHERE "Id" = {id}
                  AND NOT "IsDeleted"
                FOR UPDATE
                """)
            .IgnoreQueryFilters()
            .ToListAsync(ct);

        return payments.SingleOrDefault();
    }

    private async Task<Result<bool>> ValidateAsync(
        CreateSupplierPaymentRequest request,
        CancellationToken ct)
    {
        if (request.PaymentDate == default)
        {
            return Result<bool>.Failure(
                SupplierPaymentErrors.InvalidDate);
        }

        if (request.Amount <= 0 ||
            decimal.Round(request.Amount, 2) != request.Amount)
        {
            return Result<bool>.Failure(
                SupplierPaymentErrors.InvalidAmount);
        }

        if (request.Amount > MaxAmount)
        {
            return Result<bool>.Failure(
                SupplierPaymentErrors.AmountTooLarge);
        }

        if (!SupplierPaymentMethods.IsValid(request.PaymentMethod))
        {
            return Result<bool>.Failure(
                SupplierPaymentErrors.InvalidPaymentMethod);
        }

        if ((request.PaymentReference?.Trim().Length ?? 0) > 100 ||
            (request.Notes?.Trim().Length ?? 0) > 2000)
        {
            return Result<bool>.Failure(
                SupplierPaymentErrors.InvalidDetails);
        }

        // Inactive suppliers can still receive payments.
        var suppliers = await _db.Suppliers
            .FromSqlInterpolated($"""
                SELECT *
                FROM "Suppliers"
                WHERE "Id" = {request.SupplierId}
                  AND NOT "IsDeleted"
                FOR UPDATE
                """)
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(ct);

        if (suppliers.Count == 0)
        {
            return Result<bool>.Failure(
                SupplierPaymentErrors.SupplierUnavailable);
        }

        return Result<bool>.Success(true);
    }

    private static void ApplyDraft(
        SupplierPayment payment,
        CreateSupplierPaymentRequest request)
    {
        payment.SupplierId = request.SupplierId;
        payment.PaymentDate = request.PaymentDate;
        payment.Amount = request.Amount;
        payment.PaymentMethod = request.PaymentMethod;
        payment.PaymentReference = TrimOrNull(request.PaymentReference);
        payment.Notes = TrimOrNull(request.Notes);
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static SupplierPaymentResponse ToResponse(
        SupplierPayment payment) => new(
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
            payment.CreatedAt);
}