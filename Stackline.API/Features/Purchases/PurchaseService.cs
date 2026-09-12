using Microsoft.EntityFrameworkCore;
using Stackline.API.Common;
using Stackline.API.Data;
using Stackline.API.Data.Entities;
using Stackline.API.Features.Auth;
using Stackline.API.Purchases.Dtos;

namespace Stackline.API.Features.Purchases;

public sealed class PurchaseService : IPurchaseService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    private const decimal MaxQuantity = 999999999999999.999m;
    private const decimal MaxUnitCost = 99999999999999.9999m;
    private const decimal MaxAmount = 9999999999999999.99m;

    public PurchaseService(
        AppDbContext db,
        ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<PurchaseResponse>> CreateAsync(
        CreatePurchaseRequest request,
        CancellationToken ct)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(ct);

        var validation = await ValidateAsync(request, ct);

        if (!validation.IsSuccess)
        {
            return Result<PurchaseResponse>.Failure(validation.Error);
        }

        var now = DateTime.UtcNow;
        var id = Guid.NewGuid();

        var purchase = new Purchase
        {
            Id = id,

            PurchaseNumber = $"PUR-{id:N}",

            Status = PurchaseStatuses.Draft,
            CreatedAt = now,
            CreatedBy = _currentUser.UserId
        };

        ApplyDraft(purchase, request, validation.Value);

        _db.Purchases.Add(purchase);

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<PurchaseResponse>.Success(ToResponse(purchase));
    }

    public async Task<Result<PurchaseResponse>> UpdateAsync(
        Guid id,
        UpdatePurchaseRequest request,
        CancellationToken ct)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(ct);

        var purchase = await LockPurchaseAsync(id, ct);

        if (purchase is null)
        {
            return Result<PurchaseResponse>.Failure(
                PurchaseErrors.NotFound);
        }

        if (purchase.Status != PurchaseStatuses.Draft)
        {
            return Result<PurchaseResponse>.Failure(
                PurchaseErrors.NotDraft);
        }

        var draft = new CreatePurchaseRequest(
            request.SupplierId,
            request.WarehouseId,
            request.PurchaseDate,
            request.SupplierInvoiceNumber,
            request.Notes,
            request.Lines);

        var validation = await ValidateAsync(draft, ct);

        if (!validation.IsSuccess)
        {
            return Result<PurchaseResponse>.Failure(validation.Error);
        }

        ApplyDraft(purchase, draft, validation.Value);

        purchase.UpdatedAt = DateTime.UtcNow;
        purchase.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<PurchaseResponse>.Success(ToResponse(purchase));
    }

    public async Task<Result<Guid>> DeleteAsync(
        Guid id,
        CancellationToken ct)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(ct);

        var purchase = await LockPurchaseAsync(id, ct);

        if (purchase is null)
        {
            return Result<Guid>.Failure(PurchaseErrors.NotFound);
        }

        if (purchase.Status != PurchaseStatuses.Draft)
        {
            return Result<Guid>.Failure(PurchaseErrors.NotDraft);
        }

        purchase.IsDeleted = true;
        purchase.IsActive = false;
        purchase.UpdatedAt = DateTime.UtcNow;
        purchase.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<Guid>.Success(id);
    }

    public async Task<Result<PurchaseResponse>> PostAsync(
        Guid id,
        CancellationToken ct)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(ct);

        var purchase = await LockPurchaseAsync(id, ct);

        if (purchase is null)
        {
            return Result<PurchaseResponse>.Failure(
                PurchaseErrors.NotFound);
        }

        if (purchase.Status == PurchaseStatuses.Posted)
        {
            return Result<PurchaseResponse>.Failure(
                PurchaseErrors.AlreadyPosted);
        }

        if (purchase.Status != PurchaseStatuses.Draft)
        {
            return Result<PurchaseResponse>.Failure(
                PurchaseErrors.NotDraft);
        }

        // Revalidate references, values, and item availability.
        // Validation also locks the warehouse and selected items.
        var draft = new CreatePurchaseRequest(
            purchase.SupplierId,
            purchase.WarehouseId,
            purchase.PurchaseDate,
            purchase.SupplierInvoiceNumber,
            purchase.Notes,
            purchase.Lines.Select(line => new PurchaseLineRequest(
                line.ItemId,
                line.Quantity,
                line.UnitCost)).ToList());

        var validation = await ValidateAsync(draft, ct);

        if (!validation.IsSuccess)
        {
            return Result<PurchaseResponse>.Failure(validation.Error);
        }

        var items = validation.Value;

        foreach (var line in purchase.Lines)
        {
            if (line.UnitOfMeasure != items[line.ItemId].UnitOfMeasure)
            {
                return Result<PurchaseResponse>.Failure(
                    PurchaseErrors.ItemUnitChanged);
            }
        }

        var itemIds = purchase.Lines
            .Select(line => line.ItemId)
            .ToList();

        // Include deleted records because the existing unique index
        // covers every ItemId + WarehouseId combination.
        var stockLevels = await _db.StockLevels
            .IgnoreQueryFilters()
            .Where(stock =>
                stock.WarehouseId == purchase.WarehouseId &&
                itemIds.Contains(stock.ItemId))
            .ToDictionaryAsync(stock => stock.ItemId, ct);

        if (stockLevels.Values.Any(stock =>
            stock.IsDeleted || !stock.IsActive))
        {
            return Result<PurchaseResponse>.Failure(
                PurchaseErrors.StockUnavailable);
        }

        var now = DateTime.UtcNow;
        var userId = _currentUser.UserId;

        // Check all resulting quantities before changing any stock.
        var quantities = new Dictionary<Guid, decimal>();

        try
        {
            foreach (var line in purchase.Lines)
            {
                var currentQuantity =
                    stockLevels.TryGetValue(line.ItemId, out var stock)
                        ? stock.QuantityOnHand
                        : 0m;

                quantities[line.ItemId] =
                    checked(currentQuantity + line.Quantity);
            }
        }
        catch (OverflowException)
        {
            return Result<PurchaseResponse>.Failure(
                PurchaseErrors.AmountTooLarge);
        }

        foreach (var line in purchase.Lines.OrderBy(line => line.ItemId))
        {
            if (!stockLevels.TryGetValue(line.ItemId, out var stock))
            {
                stock = new StockLevel
                {
                    Id = Guid.NewGuid(),
                    ItemId = line.ItemId,
                    WarehouseId = purchase.WarehouseId,
                    CreatedAt = now,
                    CreatedBy = userId
                };

                _db.StockLevels.Add(stock);
            }
            else
            {
                stock.UpdatedAt = now;
                stock.UpdatedBy = userId;
            }

            stock.QuantityOnHand = quantities[line.ItemId];
            stock.LastUpdated = now;

            _db.StockMovements.Add(new StockMovement
            {
                Id = Guid.NewGuid(),
                ItemId = line.ItemId,
                WarehouseId = purchase.WarehouseId,
                MovementType = "PurchaseIn",
                Quantity = line.Quantity,
                ReferenceType = "Purchase",
                ReferenceId = purchase.Id,
                CreatedAt = now,
                CreatedBy = userId
            });

            line.LineTotal = CalculateLineTotal(
                line.Quantity,
                line.UnitCost);
        }

        purchase.TotalAmount =
            purchase.Lines.Sum(line => line.LineTotal);

        purchase.Status = PurchaseStatuses.Posted;
        purchase.PostedAt = now;
        purchase.PostedBy = userId;
        purchase.UpdatedAt = now;
        purchase.UpdatedBy = userId;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<PurchaseResponse>.Success(ToResponse(purchase));
    }

    private async Task<Purchase?> LockPurchaseAsync(
        Guid id,
        CancellationToken ct)
    {
        // Materialize the lock query before loading related lines.
        var rows = await _db.Purchases
            .FromSqlInterpolated($"""
                SELECT *
                FROM "Purchases"
                WHERE "Id" = {id}
                  AND NOT "IsDeleted"
                FOR UPDATE
                """)
            .IgnoreQueryFilters()
            .ToListAsync(ct);

        var purchase = rows.SingleOrDefault();

        if (purchase is not null)
        {
            await _db.Entry(purchase)
                .Collection(p => p.Lines)
                .LoadAsync(ct);
        }

        return purchase;
    }

    private async Task<Result<Dictionary<Guid, Item>>> ValidateAsync(
        CreatePurchaseRequest request,
        CancellationToken ct)
    {
        if (request.PurchaseDate == default)
        {
            return Result<Dictionary<Guid, Item>>.Failure(
                PurchaseErrors.InvalidDate);
        }

        if ((request.SupplierInvoiceNumber?.Trim().Length ?? 0) > 100 ||
            (request.Notes?.Trim().Length ?? 0) > 2000)
        {
            return Result<Dictionary<Guid, Item>>.Failure(
                PurchaseErrors.InvalidDetails);
        }

        if (request.Lines is null ||
            request.Lines.Count == 0 ||
            request.Lines.Any(line => line is null))
        {
            return Result<Dictionary<Guid, Item>>.Failure(
                PurchaseErrors.LinesRequired);
        }

        if (request.Lines.Select(line => line.ItemId)
            .Distinct().Count() != request.Lines.Count)
        {
            return Result<Dictionary<Guid, Item>>.Failure(
                PurchaseErrors.DuplicateItem);
        }

        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0 ||
                decimal.Round(line.Quantity, 3) != line.Quantity)
            {
                return Result<Dictionary<Guid, Item>>.Failure(
                    PurchaseErrors.InvalidQuantity);
            }

            if (line.UnitCost < 0 ||
                decimal.Round(line.UnitCost, 4) != line.UnitCost)
            {
                return Result<Dictionary<Guid, Item>>.Failure(
                    PurchaseErrors.InvalidUnitCost);
            }

            if (line.Quantity > MaxQuantity ||
                line.UnitCost > MaxUnitCost)
            {
                return Result<Dictionary<Guid, Item>>.Failure(
                    PurchaseErrors.AmountTooLarge);
            }
        }

        try
        {
            var total = request.Lines.Sum(line =>
                CalculateLineTotal(line.Quantity, line.UnitCost));

            if (total > MaxAmount)
            {
                return Result<Dictionary<Guid, Item>>.Failure(
                    PurchaseErrors.AmountTooLarge);
            }
        }
        catch (OverflowException)
        {
            return Result<Dictionary<Guid, Item>>.Failure(
                PurchaseErrors.AmountTooLarge);
        }

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
            return Result<Dictionary<Guid, Item>>.Failure(
                PurchaseErrors.WarehouseUnavailable);
        }

        var suppliers = await _db.Suppliers
            .FromSqlInterpolated($"""
                SELECT *
                FROM "Suppliers"
                WHERE "Id" = {request.SupplierId}
                  AND NOT "IsDeleted"
                  AND "IsActive"
                FOR SHARE
                """)
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(ct);

        if (suppliers.Count == 0)
        {
            return Result<Dictionary<Guid, Item>>.Failure(
                PurchaseErrors.SupplierUnavailable);
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
                return Result<Dictionary<Guid, Item>>.Failure(
                    PurchaseErrors.ItemUnavailable);
            }

            items.Add(item.Id, item);
        }

        return Result<Dictionary<Guid, Item>>.Success(items);
    }

    private void ApplyDraft(
        Purchase purchase,
        CreatePurchaseRequest request,
        Dictionary<Guid, Item> items)
    {
        purchase.SupplierId = request.SupplierId;
        purchase.WarehouseId = request.WarehouseId;
        purchase.PurchaseDate = request.PurchaseDate;
        purchase.SupplierInvoiceNumber =
            TrimOrNull(request.SupplierInvoiceNumber);
        purchase.Notes = TrimOrNull(request.Notes);

        var requestedIds = request.Lines
            .Select(line => line.ItemId)
            .ToHashSet();

        foreach (var line in purchase.Lines
            .Where(line => !requestedIds.Contains(line.ItemId))
            .ToList())
        {
            purchase.Lines.Remove(line);
            _db.PurchaseLines.Remove(line);
        }

        // Update retained lines instead of deleting and reinserting
        // them, preserving the unique PurchaseId + ItemId constraint.
        var existingLines = purchase.Lines
            .ToDictionary(line => line.ItemId);

        foreach (var input in request.Lines)
        {
            if (!existingLines.TryGetValue(input.ItemId, out var line))
            {
                line = new PurchaseLine
                {
                    Id = Guid.NewGuid(),
                    PurchaseId = purchase.Id,
                    ItemId = input.ItemId
                };

                purchase.Lines.Add(line);
            }

            var item = items[input.ItemId];

            line.SKU = item.SKU;
            line.ItemName = item.Name;
            line.UnitOfMeasure = item.UnitOfMeasure;
            line.Quantity = input.Quantity;
            line.UnitCost = input.UnitCost;
            line.LineTotal = CalculateLineTotal(
                input.Quantity,
                input.UnitCost);
        }

        purchase.TotalAmount =
            purchase.Lines.Sum(line => line.LineTotal);
    }

    private static decimal CalculateLineTotal(
        decimal quantity,
        decimal unitCost) =>
        decimal.Round(
            checked(quantity * unitCost),
            2,
            MidpointRounding.AwayFromZero);

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static PurchaseResponse ToResponse(Purchase purchase) => new(
        purchase.Id,
        purchase.PurchaseNumber,
        purchase.SupplierId,
        purchase.WarehouseId,
        purchase.PurchaseDate,
        purchase.SupplierInvoiceNumber,
        purchase.Notes,
        purchase.Status,
        purchase.TotalAmount,
        purchase.PostedAt,
        purchase.CreatedAt,
        purchase.Lines
            .OrderBy(line => line.ItemName)
            .ThenBy(line => line.Id)
            .Select(line => new PurchaseLineResponse(
                line.Id,
                line.ItemId,
                line.SKU,
                line.ItemName,
                line.UnitOfMeasure,
                line.Quantity,
                line.UnitCost,
                line.LineTotal))
            .ToList());
}