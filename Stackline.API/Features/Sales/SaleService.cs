using Microsoft.EntityFrameworkCore;
using Stackline.API.Common;
using Stackline.API.Data;
using Stackline.API.Data.Entities;
using Stackline.API.Features.Auth;
using Stackline.API.Features.Sales.Dtos;

namespace Stackline.API.Features.Sales;

public sealed class SaleService : ISaleService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    private const decimal MaxQuantity = 999999999999999.999m;
    private const decimal MaxUnitPrice = 99999999999999.9999m;
    private const decimal MaxAmount = 9999999999999999.99m;

    private sealed record ValidatedSale(
        Customer Customer,
        Dictionary<Guid, Item> Items,
        decimal TotalAmount);

    public SaleService(
        AppDbContext db,
        ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<SaleResponse>> CreateAsync(
        CreateSaleRequest request,
        CancellationToken ct)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(ct);

        var validation = await ValidateAsync(request, ct);

        if (!validation.IsSuccess)
        {
            return Result<SaleResponse>.Failure(validation.Error);
        }

        var id = Guid.NewGuid();

        var sale = new Sale
        {
            Id = id,
            SaleNumber = $"SAL-{id:N}",
            Status = SaleStatuses.Draft,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        _db.Sales.Add(sale);

        ApplyDraft(sale, request, validation.Value);

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<SaleResponse>.Success(ToResponse(sale));
    }

    public async Task<Result<SaleResponse>> UpdateAsync(
        Guid id,
        UpdateSaleRequest request,
        CancellationToken ct)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(ct);

        var sale = await LockSaleAsync(id, ct);

        if (sale is null)
        {
            return Result<SaleResponse>.Failure(SaleErrors.NotFound);
        }

        if (sale.Status != SaleStatuses.Draft)
        {
            return Result<SaleResponse>.Failure(SaleErrors.NotDraft);
        }

        var draft = new CreateSaleRequest(
            request.CustomerId,
            request.WarehouseId,
            request.SaleDate,
            request.Notes,
            request.AmountPaid,
            request.Lines);

        var validation = await ValidateAsync(draft, ct);

        if (!validation.IsSuccess)
        {
            return Result<SaleResponse>.Failure(validation.Error);
        }

        ApplyDraft(sale, draft, validation.Value);

        sale.UpdatedAt = DateTime.UtcNow;
        sale.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<SaleResponse>.Success(ToResponse(sale));
    }

    public async Task<Result<Guid>> DeleteAsync(
        Guid id,
        CancellationToken ct)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(ct);

        var sale = await LockSaleAsync(id, ct);

        if (sale is null)
        {
            return Result<Guid>.Failure(SaleErrors.NotFound);
        }

        if (sale.Status != SaleStatuses.Draft)
        {
            return Result<Guid>.Failure(SaleErrors.NotDraft);
        }

        sale.IsDeleted = true;
        sale.IsActive = false;
        sale.UpdatedAt = DateTime.UtcNow;
        sale.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<Guid>.Success(id);
    }

    public async Task<Result<SaleResponse>> PostAsync(
        Guid id,
        CancellationToken ct)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(ct);

        var sale = await LockSaleAsync(id, ct);

        if (sale is null)
        {
            return Result<SaleResponse>.Failure(SaleErrors.NotFound);
        }

        if (sale.Status == SaleStatuses.Posted)
        {
            return Result<SaleResponse>.Failure(SaleErrors.AlreadyPosted);
        }

        if (sale.Status != SaleStatuses.Draft)
        {
            return Result<SaleResponse>.Failure(SaleErrors.NotDraft);
        }

        var draft = new CreateSaleRequest(
            sale.CustomerId,
            sale.WarehouseId,
            sale.SaleDate,
            sale.Notes,
            sale.AmountPaid,
            sale.Lines.Select(line => new SaleLineRequest(
                line.ItemId,
                line.Quantity,
                line.UnitPrice)).ToList());

        // Recheck all values and active references at posting time.
        // Also acquires warehouse, customer, and item locks.
        var validation = await ValidateAsync(draft, ct);

        if (!validation.IsSuccess)
        {
            return Result<SaleResponse>.Failure(validation.Error);
        }

        var validated = validation.Value;

        foreach (var line in sale.Lines)
        {
            if (line.UnitOfMeasure !=
                validated.Items[line.ItemId].UnitOfMeasure)
            {
                return Result<SaleResponse>.Failure(
                    SaleErrors.ItemUnitChanged);
            }
        }

        var itemIds = sale.Lines
            .Select(line => line.ItemId)
            .ToList();

        var stocks = await _db.StockLevels
            .IgnoreQueryFilters()
            .Where(stock =>
                stock.WarehouseId == sale.WarehouseId &&
                itemIds.Contains(stock.ItemId))
            .ToDictionaryAsync(stock => stock.ItemId, ct);

        // Check every line before changing any stock.
        foreach (var line in sale.Lines)
        {
            if (!stocks.TryGetValue(line.ItemId, out var stock) ||
                stock.IsDeleted ||
                !stock.IsActive ||
                stock.QuantityOnHand < line.Quantity)
            {
                return Result<SaleResponse>.Failure(
                    SaleErrors.InsufficientStock);
            }
        }

        var unpaidAmount =
            validated.TotalAmount - sale.AmountPaid;

        // Fully paid sales do not increase outstanding credit.
        if (unpaidAmount > 0)
        {
            decimal resultingBalance;

            try
            {
                // Include every posted sale, even if it was incorrectly
                // soft-deleted elsewhere, so debt is not silently lost.
                var existingUnpaid = await _db.Sales
                    .IgnoreQueryFilters()
                    .Where(existing =>
                        existing.CustomerId == sale.CustomerId &&
                        existing.Status == SaleStatuses.Posted)
                    .SumAsync(
                        existing =>
                            (decimal?)(existing.TotalAmount -
                                       existing.AmountPaid),
                        ct) ?? 0m;

                resultingBalance = checked(
                    validated.Customer.OpeningBalance +
                    existingUnpaid +
                    unpaidAmount);
            }
            catch (OverflowException)
            {
                return Result<SaleResponse>.Failure(
                    SaleErrors.AmountTooLarge);
            }

            if (resultingBalance > validated.Customer.CreditLimit)
            {
                return Result<SaleResponse>.Failure(
                    SaleErrors.CreditLimitExceeded);
            }
        }

        var now = DateTime.UtcNow;
        var userId = _currentUser.UserId;

        foreach (var line in sale.Lines.OrderBy(line => line.ItemId))
        {
            var stock = stocks[line.ItemId];

            stock.QuantityOnHand -= line.Quantity;
            stock.LastUpdated = now;
            stock.UpdatedAt = now;
            stock.UpdatedBy = userId;

            _db.StockMovements.Add(new StockMovement
            {
                Id = Guid.NewGuid(),
                ItemId = line.ItemId,
                WarehouseId = sale.WarehouseId,
                MovementType = "SaleOut",

                // Quantity is the magnitude; SaleOut indicates direction.
                Quantity = line.Quantity,

                ReferenceType = "Sale",
                ReferenceId = sale.Id,
                CreatedAt = now,
                CreatedBy = userId
            });

            line.LineTotal = CalculateLineTotal(
                line.Quantity,
                line.UnitPrice);
        }

        sale.TotalAmount = validated.TotalAmount;
        sale.Status = SaleStatuses.Posted;
        sale.PostedAt = now;
        sale.PostedBy = userId;
        sale.UpdatedAt = now;
        sale.UpdatedBy = userId;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<SaleResponse>.Success(ToResponse(sale));
    }

    private async Task<Sale?> LockSaleAsync(
        Guid id,
        CancellationToken ct)
    {
        var rows = await _db.Sales
            .FromSqlInterpolated($"""
                SELECT *
                FROM "Sales"
                WHERE "Id" = {id}
                  AND NOT "IsDeleted"
                FOR UPDATE
                """)
            .IgnoreQueryFilters()
            .ToListAsync(ct);

        var sale = rows.SingleOrDefault();

        if (sale is not null)
        {
            await _db.Entry(sale)
                .Collection(s => s.Lines)
                .LoadAsync(ct);
        }

        return sale;
    }

    private async Task<Result<ValidatedSale>> ValidateAsync(
        CreateSaleRequest request,
        CancellationToken ct)
    {
        if (request.SaleDate == default)
        {
            return Result<ValidatedSale>.Failure(
                SaleErrors.InvalidDate);
        }

        if ((request.Notes?.Trim().Length ?? 0) > 2000)
        {
            return Result<ValidatedSale>.Failure(
                SaleErrors.InvalidNotes);
        }

        if (request.Lines is null ||
            request.Lines.Count == 0 ||
            request.Lines.Any(line => line is null))
        {
            return Result<ValidatedSale>.Failure(
                SaleErrors.LinesRequired);
        }

        if (request.Lines.Select(line => line.ItemId)
            .Distinct().Count() != request.Lines.Count)
        {
            return Result<ValidatedSale>.Failure(
                SaleErrors.DuplicateItem);
        }

        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0 ||
                decimal.Round(line.Quantity, 3) != line.Quantity)
            {
                return Result<ValidatedSale>.Failure(
                    SaleErrors.InvalidQuantity);
            }

            if (line.UnitPrice < 0 ||
                decimal.Round(line.UnitPrice, 4) != line.UnitPrice)
            {
                return Result<ValidatedSale>.Failure(
                    SaleErrors.InvalidUnitPrice);
            }

            if (line.Quantity > MaxQuantity ||
                line.UnitPrice > MaxUnitPrice)
            {
                return Result<ValidatedSale>.Failure(
                    SaleErrors.AmountTooLarge);
            }
        }

        decimal total;

        try
        {
            total = request.Lines.Sum(line =>
                CalculateLineTotal(line.Quantity, line.UnitPrice));
        }
        catch (OverflowException)
        {
            return Result<ValidatedSale>.Failure(
                SaleErrors.AmountTooLarge);
        }

        if (total > MaxAmount)
        {
            return Result<ValidatedSale>.Failure(
                SaleErrors.AmountTooLarge);
        }

        if (request.AmountPaid < 0 ||
            request.AmountPaid > total ||
            decimal.Round(request.AmountPaid, 2) != request.AmountPaid)
        {
            return Result<ValidatedSale>.Failure(
                SaleErrors.InvalidPayment);
        }

        // Same warehouse lock used by purchase posting and deletion.
        var warehouses = await _db.Warehouses
            .FromSqlInterpolated($"""
                SELECT *
                FROM "Warehouses"
                WHERE "Id" = {request.WarehouseId}
                  AND NOT "IsDeleted"
                  AND "IsActive"
                FOR UPDATE
                """)
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(ct);

        if (warehouses.Count == 0)
        {
            return Result<ValidatedSale>.Failure(
                SaleErrors.WarehouseUnavailable);
        }

        // Serializes credit checks for the same customer,
        // including sales from different warehouses.
        var customers = await _db.Customers
            .FromSqlInterpolated($"""
                SELECT *
                FROM "Customers"
                WHERE "Id" = {request.CustomerId}
                  AND NOT "IsDeleted"
                  AND "IsActive"
                FOR UPDATE
                """)
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(ct);

        var customer = customers.SingleOrDefault();

        if (customer is null)
        {
            return Result<ValidatedSale>.Failure(
                SaleErrors.CustomerUnavailable);
        }

        var items = new Dictionary<Guid, Item>();

        foreach (var itemId in request.Lines
            .Select(line => line.ItemId)
            .OrderBy(id => id))
        {
            var matches = await _db.Items
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM "Items"
                    WHERE "Id" = {itemId}
                      AND NOT "IsDeleted"
                      AND "IsActive"
                    FOR UPDATE
                    """)
                .IgnoreQueryFilters()
                .AsNoTracking()
                .ToListAsync(ct);

            var item = matches.SingleOrDefault();

            if (item is null)
            {
                return Result<ValidatedSale>.Failure(
                    SaleErrors.ItemUnavailable);
            }

            items.Add(item.Id, item);
        }

        return Result<ValidatedSale>.Success(
            new ValidatedSale(customer, items, total));
    }

    private void ApplyDraft(
        Sale sale,
        CreateSaleRequest request,
        ValidatedSale validated)
    {
        sale.CustomerId = request.CustomerId;
        sale.WarehouseId = request.WarehouseId;
        sale.SaleDate = request.SaleDate;
        sale.Notes = string.IsNullOrWhiteSpace(request.Notes)
            ? null
            : request.Notes.Trim();
        sale.AmountPaid = request.AmountPaid;

        var requestedIds = request.Lines
            .Select(line => line.ItemId)
            .ToHashSet();

        foreach (var line in sale.Lines
            .Where(line => !requestedIds.Contains(line.ItemId))
            .ToList())
        {
            sale.Lines.Remove(line);
            _db.SaleLines.Remove(line);
        }

        var existingLines = sale.Lines
            .ToDictionary(line => line.ItemId);

        foreach (var input in request.Lines)
        {
            if (!existingLines.TryGetValue(input.ItemId, out var line))
            {
                line = new SaleLine
                {
                    Id = Guid.NewGuid(),
                    SaleId = sale.Id,
                    Sale = sale,
                    ItemId = input.ItemId
                };

                sale.Lines.Add(line);

                // Explicitly mark newly added lines as Added.
                _db.SaleLines.Add(line);
            }

            var item = validated.Items[input.ItemId];

            line.SKU = item.SKU;
            line.ItemName = item.Name;
            line.UnitOfMeasure = item.UnitOfMeasure;
            line.Quantity = input.Quantity;
            line.UnitPrice = input.UnitPrice;
            line.LineTotal = CalculateLineTotal(
                input.Quantity,
                input.UnitPrice);
        }

        sale.TotalAmount = validated.TotalAmount;
    }

    private static decimal CalculateLineTotal(
        decimal quantity,
        decimal unitPrice) =>
        decimal.Round(
            checked(quantity * unitPrice),
            2,
            MidpointRounding.AwayFromZero);

    public static SaleResponse ToResponse(Sale sale) => new(
        sale.Id,
        sale.SaleNumber,
        sale.CustomerId,
        sale.WarehouseId,
        sale.SaleDate,
        sale.Notes,
        sale.Status,
        sale.TotalAmount,
        sale.AmountPaid,
        sale.TotalAmount - sale.AmountPaid,
        sale.PostedAt,
        sale.CreatedAt,
        sale.Lines
            .OrderBy(line => line.ItemName)
            .ThenBy(line => line.Id)
            .Select(line => new SaleLineResponse(
                line.Id,
                line.ItemId,
                line.SKU,
                line.ItemName,
                line.UnitOfMeasure,
                line.Quantity,
                line.UnitPrice,
                line.LineTotal))
            .ToList());
}