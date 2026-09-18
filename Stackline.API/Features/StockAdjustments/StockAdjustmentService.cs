using System.Data;
using Microsoft.EntityFrameworkCore;
using Stackline.API.Common;
using Stackline.API.Data;
using Stackline.API.Data.Entities;
using Stackline.API.Features.Auth;
using Stackline.API.Features.StockAdjustments.Dtos;

namespace Stackline.API.Features.StockAdjustments;

public sealed class StockAdjustmentService : IStockAdjustmentService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    private const decimal MaxQuantity = 999999999999999.999m;

    public StockAdjustmentService(
        AppDbContext db,
        ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<StockAdjustmentResponse>> CreateAsync(
        SaveStockAdjustmentRequest request,
        CancellationToken ct)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, ct);

        var validation = await ValidateAsync(request, ct);

        if (!validation.IsSuccess)
        {
            return Result<StockAdjustmentResponse>.Failure(
                validation.Error);
        }

        var id = Guid.NewGuid();

        var adjustment = new StockAdjustment
        {
            Id = id,
            AdjustmentNumber = $"ADJ-{id:N}",
            Status = StockAdjustmentStatuses.Draft,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        _db.StockAdjustments.Add(adjustment);

        ApplyDraft(adjustment, request, validation.Value);

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<StockAdjustmentResponse>.Success(
            ToResponse(adjustment));
    }

    public async Task<Result<StockAdjustmentResponse>> UpdateAsync(
        Guid id,
        SaveStockAdjustmentRequest request,
        CancellationToken ct)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, ct);

        var adjustment = await LockAdjustmentAsync(id, ct);

        if (adjustment is null)
        {
            return Result<StockAdjustmentResponse>.Failure(
                StockAdjustmentErrors.NotFound);
        }

        if (adjustment.Status != StockAdjustmentStatuses.Draft)
        {
            return Result<StockAdjustmentResponse>.Failure(
                StockAdjustmentErrors.NotDraft);
        }

        var validation = await ValidateAsync(request, ct);

        if (!validation.IsSuccess)
        {
            return Result<StockAdjustmentResponse>.Failure(
                validation.Error);
        }

        ApplyDraft(adjustment, request, validation.Value);

        adjustment.UpdatedAt = DateTime.UtcNow;
        adjustment.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<StockAdjustmentResponse>.Success(
            ToResponse(adjustment));
    }

    public async Task<Result<Guid>> DeleteAsync(
        Guid id,
        CancellationToken ct)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, ct);

        var adjustment = await LockAdjustmentAsync(id, ct);

        if (adjustment is null)
        {
            return Result<Guid>.Failure(
                StockAdjustmentErrors.NotFound);
        }

        if (adjustment.Status != StockAdjustmentStatuses.Draft)
        {
            return Result<Guid>.Failure(
                StockAdjustmentErrors.NotDraft);
        }

        adjustment.IsDeleted = true;
        adjustment.IsActive = false;
        adjustment.UpdatedAt = DateTime.UtcNow;
        adjustment.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<Guid>.Success(id);
    }

    public async Task<Result<StockAdjustmentResponse>> PostAsync(
        Guid id,
        CancellationToken ct)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, ct);

        var adjustment = await LockAdjustmentAsync(id, ct);

        if (adjustment is null)
        {
            return Result<StockAdjustmentResponse>.Failure(
                StockAdjustmentErrors.NotFound);
        }

        if (adjustment.Status == StockAdjustmentStatuses.Posted)
        {
            return Result<StockAdjustmentResponse>.Failure(
                StockAdjustmentErrors.AlreadyPosted);
        }

        if (adjustment.Status != StockAdjustmentStatuses.Draft)
        {
            return Result<StockAdjustmentResponse>.Failure(
                StockAdjustmentErrors.NotDraft);
        }

        var request = new SaveStockAdjustmentRequest(
            adjustment.WarehouseId,
            adjustment.AdjustmentDate,
            adjustment.Reason,
            adjustment.Notes,
            adjustment.Lines
                .Select(line => new StockAdjustmentLineRequest(
                    line.ItemId,
                    line.Direction,
                    line.Quantity))
                .ToList());

        // Revalidates references and locks warehouse, then ordered items.
        var validation = await ValidateAsync(request, ct);

        if (!validation.IsSuccess)
        {
            return Result<StockAdjustmentResponse>.Failure(
                validation.Error);
        }

        foreach (var line in adjustment.Lines)
        {
            if (line.UnitOfMeasure !=
                validation.Value[line.ItemId].UnitOfMeasure)
            {
                return Result<StockAdjustmentResponse>.Failure(
                    StockAdjustmentErrors.ItemUnitChanged);
            }
        }

        var itemIds = adjustment.Lines
            .Select(line => line.ItemId)
            .ToList();

        var stocks = await _db.StockLevels
            .IgnoreQueryFilters()
            .Where(stock =>
                stock.WarehouseId == adjustment.WarehouseId &&
                itemIds.Contains(stock.ItemId))
            .ToDictionaryAsync(stock => stock.ItemId, ct);

        var resultingQuantities = new Dictionary<Guid, decimal>();

        // Validate the complete document before changing tracked stock.
        foreach (var line in adjustment.Lines)
        {
            stocks.TryGetValue(line.ItemId, out var stock);

            if (stock is not null &&
                (stock.IsDeleted || !stock.IsActive))
            {
                return Result<StockAdjustmentResponse>.Failure(
                    StockAdjustmentErrors.StockUnavailable);
            }

            var currentQuantity = stock?.QuantityOnHand ?? 0m;

            if (currentQuantity < 0 || currentQuantity > MaxQuantity)
            {
                return Result<StockAdjustmentResponse>.Failure(
                    StockAdjustmentErrors.StockOutOfRange);
            }

            if (line.Direction == StockAdjustmentDirections.Decrease &&
                currentQuantity < line.Quantity)
            {
                return Result<StockAdjustmentResponse>.Failure(
                    StockAdjustmentErrors.InsufficientStock);
            }

            // Check the upper limit before adding.
            if (line.Direction == StockAdjustmentDirections.Increase &&
                currentQuantity > MaxQuantity - line.Quantity)
            {
                return Result<StockAdjustmentResponse>.Failure(
                    StockAdjustmentErrors.StockOutOfRange);
            }

            resultingQuantities[line.ItemId] =
                line.Direction == StockAdjustmentDirections.Increase
                    ? currentQuantity + line.Quantity
                    : currentQuantity - line.Quantity;
        }

        var now = DateTime.UtcNow;
        var userId = _currentUser.UserId;

        foreach (var line in adjustment.Lines.OrderBy(l => l.ItemId))
        {
            if (!stocks.TryGetValue(line.ItemId, out var stock))
            {
                stock = new StockLevel
                {
                    Id = Guid.NewGuid(),
                    ItemId = line.ItemId,
                    WarehouseId = adjustment.WarehouseId,
                    CreatedAt = now,
                    CreatedBy = userId
                };

                _db.StockLevels.Add(stock);
                stocks.Add(line.ItemId, stock);
            }

            stock.QuantityOnHand = resultingQuantities[line.ItemId];
            stock.LastUpdated = now;
            stock.UpdatedAt = now;
            stock.UpdatedBy = userId;

            _db.StockMovements.Add(new StockMovement
            {
                Id = Guid.NewGuid(),
                ItemId = line.ItemId,
                WarehouseId = adjustment.WarehouseId,
                MovementType =
                    line.Direction == StockAdjustmentDirections.Increase
                        ? "AdjustmentIn"
                        : "AdjustmentOut",

                // Matches SaleOut: quantity is a positive magnitude.
                Quantity = line.Quantity,
                ReferenceType = "StockAdjustment",
                ReferenceId = adjustment.Id,
                CreatedAt = now,
                CreatedBy = userId
            });
        }

        adjustment.Status = StockAdjustmentStatuses.Posted;
        adjustment.PostedAt = now;
        adjustment.PostedBy = userId;
        adjustment.UpdatedAt = now;
        adjustment.UpdatedBy = userId;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<StockAdjustmentResponse>.Success(
            ToResponse(adjustment));
    }

    private async Task<StockAdjustment?> LockAdjustmentAsync(
        Guid id,
        CancellationToken ct)
    {
        var rows = await _db.StockAdjustments
            .FromSqlInterpolated($"""
                SELECT *
                FROM "StockAdjustments"
                WHERE "Id" = {id}
                  AND NOT "IsDeleted"
                FOR UPDATE
                """)
            .IgnoreQueryFilters()
            .ToListAsync(ct);

        var adjustment = rows.SingleOrDefault();

        if (adjustment is not null)
        {
            await _db.Entry(adjustment)
                .Collection(a => a.Lines)
                .LoadAsync(ct);
        }

        return adjustment;
    }

    private async Task<Result<Dictionary<Guid, Item>>> ValidateAsync(
        SaveStockAdjustmentRequest request,
        CancellationToken ct)
    {
        if (request.AdjustmentDate == default)
        {
            return Result<Dictionary<Guid, Item>>.Failure(
                StockAdjustmentErrors.InvalidDate);
        }

        if (!StockAdjustmentReasons.IsValid(request.Reason))
        {
            return Result<Dictionary<Guid, Item>>.Failure(
                StockAdjustmentErrors.InvalidReason);
        }

        if ((request.Notes?.Trim().Length ?? 0) > 2000)
        {
            return Result<Dictionary<Guid, Item>>.Failure(
                StockAdjustmentErrors.InvalidNotes);
        }

        if (request.Reason == StockAdjustmentReasons.Other &&
            string.IsNullOrWhiteSpace(request.Notes))
        {
            return Result<Dictionary<Guid, Item>>.Failure(
                StockAdjustmentErrors.NotesRequired);
        }

        if (request.Lines is null ||
            request.Lines.Count == 0 ||
            request.Lines.Any(line => line is null))
        {
            return Result<Dictionary<Guid, Item>>.Failure(
                StockAdjustmentErrors.LinesRequired);
        }

        if (request.Lines.Select(line => line.ItemId)
            .Distinct().Count() != request.Lines.Count)
        {
            return Result<Dictionary<Guid, Item>>.Failure(
                StockAdjustmentErrors.DuplicateItem);
        }

        foreach (var line in request.Lines)
        {
            if (!StockAdjustmentDirections.IsValid(line.Direction))
            {
                return Result<Dictionary<Guid, Item>>.Failure(
                    StockAdjustmentErrors.InvalidDirection);
            }

            if (!StockAdjustmentReasons.AllowsDirection(
                request.Reason, line.Direction))
            {
                return Result<Dictionary<Guid, Item>>.Failure(
                    StockAdjustmentErrors.DirectionNotAllowed);
            }

            if (line.Quantity <= 0 ||
                line.Quantity > MaxQuantity ||
                decimal.Round(line.Quantity, 3) != line.Quantity)
            {
                return Result<Dictionary<Guid, Item>>.Failure(
                    StockAdjustmentErrors.InvalidQuantity);
            }
        }

        // Uses the same lock order as purchase and sale posting.
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
                StockAdjustmentErrors.WarehouseUnavailable);
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
                    StockAdjustmentErrors.ItemUnavailable);
            }

            items.Add(item.Id, item);
        }

        return Result<Dictionary<Guid, Item>>.Success(items);
    }

    private void ApplyDraft(
        StockAdjustment adjustment,
        SaveStockAdjustmentRequest request,
        Dictionary<Guid, Item> items)
    {
        adjustment.WarehouseId = request.WarehouseId;
        adjustment.AdjustmentDate = request.AdjustmentDate;
        adjustment.Reason = request.Reason;
        adjustment.Notes = string.IsNullOrWhiteSpace(request.Notes)
            ? null
            : request.Notes.Trim();

        var requestedIds = request.Lines
            .Select(line => line.ItemId)
            .ToHashSet();

        foreach (var line in adjustment.Lines
            .Where(line => !requestedIds.Contains(line.ItemId))
            .ToList())
        {
            adjustment.Lines.Remove(line);
            _db.StockAdjustmentLines.Remove(line);
        }

        var existingLines = adjustment.Lines
            .ToDictionary(line => line.ItemId);

        foreach (var input in request.Lines)
        {
            if (!existingLines.TryGetValue(input.ItemId, out var line))
            {
                line = new StockAdjustmentLine
                {
                    Id = Guid.NewGuid(),
                    StockAdjustmentId = adjustment.Id,
                    StockAdjustment = adjustment,
                    ItemId = input.ItemId
                };

                adjustment.Lines.Add(line);
                _db.StockAdjustmentLines.Add(line);
            }

            var item = items[input.ItemId];

            line.SKU = item.SKU;
            line.ItemName = item.Name;
            line.UnitOfMeasure = item.UnitOfMeasure;
            line.Direction = input.Direction;
            line.Quantity = input.Quantity;
        }
    }

    public static StockAdjustmentResponse ToResponse(
        StockAdjustment adjustment) => new(
        adjustment.Id,
        adjustment.AdjustmentNumber,
        adjustment.WarehouseId,
        adjustment.AdjustmentDate,
        adjustment.Reason,
        adjustment.Notes,
        adjustment.Status,
        adjustment.PostedAt,
        adjustment.CreatedAt,
        adjustment.Lines
            .OrderBy(line => line.ItemName)
            .ThenBy(line => line.Id)
            .Select(line => new StockAdjustmentLineResponse(
                line.Id,
                line.ItemId,
                line.SKU,
                line.ItemName,
                line.UnitOfMeasure,
                line.Direction,
                line.Quantity))
            .ToList());
}