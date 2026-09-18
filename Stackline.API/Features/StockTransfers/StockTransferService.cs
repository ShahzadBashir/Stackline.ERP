using System.Data;
using Microsoft.EntityFrameworkCore;
using Stackline.API.Common;
using Stackline.API.Data;
using Stackline.API.Data.Entities;
using Stackline.API.Features.Auth;
using Stackline.API.Features.StockTransfers.Dtos;

namespace Stackline.API.Features.StockTransfers;

public sealed class StockTransferService : IStockTransferService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    private const decimal MaxQuantity = 999999999999999.999m;

    public StockTransferService(
        AppDbContext db,
        ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<StockTransferResponse>> CreateAsync(
        SaveStockTransferRequest request,
        CancellationToken ct)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, ct);

        var validation = await ValidateAsync(request, ct);

        if (!validation.IsSuccess)
        {
            return Result<StockTransferResponse>.Failure(
                validation.Error);
        }

        var id = Guid.NewGuid();

        var transfer = new StockTransfer
        {
            Id = id,
            TransferNumber = $"TRF-{id:N}",
            Status = StockTransferStatuses.Draft,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        _db.StockTransfers.Add(transfer);

        ApplyDraft(transfer, request, validation.Value);

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<StockTransferResponse>.Success(
            ToResponse(transfer));
    }

    public async Task<Result<StockTransferResponse>> UpdateAsync(
        Guid id,
        SaveStockTransferRequest request,
        CancellationToken ct)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, ct);

        var transfer = await LockTransferAsync(id, ct);

        if (transfer is null)
        {
            return Result<StockTransferResponse>.Failure(
                StockTransferErrors.NotFound);
        }

        if (transfer.Status != StockTransferStatuses.Draft)
        {
            return Result<StockTransferResponse>.Failure(
                StockTransferErrors.NotDraft);
        }

        var validation = await ValidateAsync(request, ct);

        if (!validation.IsSuccess)
        {
            return Result<StockTransferResponse>.Failure(
                validation.Error);
        }

        ApplyDraft(transfer, request, validation.Value);

        transfer.UpdatedAt = DateTime.UtcNow;
        transfer.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<StockTransferResponse>.Success(
            ToResponse(transfer));
    }

    public async Task<Result<Guid>> DeleteAsync(
        Guid id,
        CancellationToken ct)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, ct);

        var transfer = await LockTransferAsync(id, ct);

        if (transfer is null)
        {
            return Result<Guid>.Failure(StockTransferErrors.NotFound);
        }

        if (transfer.Status != StockTransferStatuses.Draft)
        {
            return Result<Guid>.Failure(StockTransferErrors.NotDraft);
        }

        transfer.IsDeleted = true;
        transfer.IsActive = false;
        transfer.UpdatedAt = DateTime.UtcNow;
        transfer.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<Guid>.Success(id);
    }

    public async Task<Result<StockTransferResponse>> PostAsync(
        Guid id,
        CancellationToken ct)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, ct);

        var transfer = await LockTransferAsync(id, ct);

        if (transfer is null)
        {
            return Result<StockTransferResponse>.Failure(
                StockTransferErrors.NotFound);
        }

        if (transfer.Status == StockTransferStatuses.Posted)
        {
            return Result<StockTransferResponse>.Failure(
                StockTransferErrors.AlreadyPosted);
        }

        if (transfer.Status != StockTransferStatuses.Draft)
        {
            return Result<StockTransferResponse>.Failure(
                StockTransferErrors.NotDraft);
        }

        var request = new SaveStockTransferRequest(
            transfer.SourceWarehouseId,
            transfer.DestinationWarehouseId,
            transfer.TransferDate,
            transfer.Notes,
            transfer.Lines
                .Select(line => new StockTransferLineRequest(
                    line.ItemId,
                    line.Quantity))
                .ToList());

        // Revalidates the draft and locks both warehouses, then items.
        var validation = await ValidateAsync(request, ct);

        if (!validation.IsSuccess)
        {
            return Result<StockTransferResponse>.Failure(
                validation.Error);
        }

        foreach (var line in transfer.Lines)
        {
            if (line.UnitOfMeasure !=
                validation.Value[line.ItemId].UnitOfMeasure)
            {
                return Result<StockTransferResponse>.Failure(
                    StockTransferErrors.ItemUnitChanged);
            }
        }

        var itemIds = transfer.Lines
            .Select(line => line.ItemId)
            .ToList();

        var stockRows = await _db.StockLevels
            .IgnoreQueryFilters()
            .Where(stock =>
                itemIds.Contains(stock.ItemId) &&
                (stock.WarehouseId == transfer.SourceWarehouseId ||
                 stock.WarehouseId == transfer.DestinationWarehouseId))
            .ToListAsync(ct);

        var sourceStocks = stockRows
            .Where(stock =>
                stock.WarehouseId == transfer.SourceWarehouseId)
            .ToDictionary(stock => stock.ItemId);

        var destinationStocks = stockRows
            .Where(stock =>
                stock.WarehouseId == transfer.DestinationWarehouseId)
            .ToDictionary(stock => stock.ItemId);

        // Validate every line before applying any changes.
        foreach (var line in transfer.Lines)
        {
            sourceStocks.TryGetValue(line.ItemId, out var source);
            destinationStocks.TryGetValue(
                line.ItemId, out var destination);

            if ((source is not null &&
                 (source.IsDeleted || !source.IsActive)) ||
                (destination is not null &&
                 (destination.IsDeleted || !destination.IsActive)))
            {
                return Result<StockTransferResponse>.Failure(
                    StockTransferErrors.StockUnavailable);
            }

            var sourceQuantity = source?.QuantityOnHand ?? 0m;
            var destinationQuantity =
                destination?.QuantityOnHand ?? 0m;

            if (sourceQuantity < 0 ||
                sourceQuantity > MaxQuantity ||
                destinationQuantity < 0 ||
                destinationQuantity > MaxQuantity)
            {
                return Result<StockTransferResponse>.Failure(
                    StockTransferErrors.StockOutOfRange);
            }

            if (sourceQuantity < line.Quantity)
            {
                return Result<StockTransferResponse>.Failure(
                    StockTransferErrors.InsufficientStock);
            }

            if (destinationQuantity > MaxQuantity - line.Quantity)
            {
                return Result<StockTransferResponse>.Failure(
                    StockTransferErrors.StockOutOfRange);
            }
        }

        var now = DateTime.UtcNow;
        var userId = _currentUser.UserId;

        foreach (var line in transfer.Lines.OrderBy(l => l.ItemId))
        {
            // Validation guarantees the source record exists.
            var source = sourceStocks[line.ItemId];

            if (!destinationStocks.TryGetValue(
                line.ItemId, out var destination))
            {
                destination = new StockLevel
                {
                    Id = Guid.NewGuid(),
                    ItemId = line.ItemId,
                    WarehouseId = transfer.DestinationWarehouseId,
                    CreatedAt = now,
                    CreatedBy = userId
                };

                _db.StockLevels.Add(destination);
                destinationStocks.Add(line.ItemId, destination);
            }

            source.QuantityOnHand -= line.Quantity;
            source.LastUpdated = now;
            source.UpdatedAt = now;
            source.UpdatedBy = userId;

            destination.QuantityOnHand += line.Quantity;
            destination.LastUpdated = now;
            destination.UpdatedAt = now;
            destination.UpdatedBy = userId;

            _db.StockMovements.AddRange(
                new StockMovement
                {
                    Id = Guid.NewGuid(),
                    ItemId = line.ItemId,
                    WarehouseId = transfer.SourceWarehouseId,
                    MovementType = "TransferOut",
                    Quantity = line.Quantity,
                    ReferenceType = "StockTransfer",
                    ReferenceId = transfer.Id,
                    CreatedAt = now,
                    CreatedBy = userId
                },
                new StockMovement
                {
                    Id = Guid.NewGuid(),
                    ItemId = line.ItemId,
                    WarehouseId = transfer.DestinationWarehouseId,
                    MovementType = "TransferIn",
                    Quantity = line.Quantity,
                    ReferenceType = "StockTransfer",
                    ReferenceId = transfer.Id,
                    CreatedAt = now,
                    CreatedBy = userId
                });
        }

        transfer.Status = StockTransferStatuses.Posted;
        transfer.PostedAt = now;
        transfer.PostedBy = userId;
        transfer.UpdatedAt = now;
        transfer.UpdatedBy = userId;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<StockTransferResponse>.Success(
            ToResponse(transfer));
    }

    private async Task<StockTransfer?> LockTransferAsync(
        Guid id,
        CancellationToken ct)
    {
        var rows = await _db.StockTransfers
            .FromSqlInterpolated($"""
                SELECT *
                FROM "StockTransfers"
                WHERE "Id" = {id}
                  AND NOT "IsDeleted"
                FOR UPDATE
                """)
            .IgnoreQueryFilters()
            .ToListAsync(ct);

        var transfer = rows.SingleOrDefault();

        if (transfer is not null)
        {
            await _db.Entry(transfer)
                .Collection(t => t.Lines)
                .LoadAsync(ct);
        }

        return transfer;
    }

    private async Task<Result<Dictionary<Guid, Item>>> ValidateAsync(
        SaveStockTransferRequest request,
        CancellationToken ct)
    {
        if (request.TransferDate == default)
        {
            return Result<Dictionary<Guid, Item>>.Failure(
                StockTransferErrors.InvalidDate);
        }

        if ((request.Notes?.Trim().Length ?? 0) > 2000)
        {
            return Result<Dictionary<Guid, Item>>.Failure(
                StockTransferErrors.InvalidNotes);
        }

        if (request.SourceWarehouseId == Guid.Empty ||
            request.DestinationWarehouseId == Guid.Empty)
        {
            return Result<Dictionary<Guid, Item>>.Failure(
                StockTransferErrors.WarehousesRequired);
        }

        if (request.SourceWarehouseId == request.DestinationWarehouseId)
        {
            return Result<Dictionary<Guid, Item>>.Failure(
                StockTransferErrors.SameWarehouse);
        }

        if (request.Lines is null ||
            request.Lines.Count == 0 ||
            request.Lines.Any(line => line is null))
        {
            return Result<Dictionary<Guid, Item>>.Failure(
                StockTransferErrors.LinesRequired);
        }

        if (request.Lines.Select(line => line.ItemId)
            .Distinct().Count() != request.Lines.Count)
        {
            return Result<Dictionary<Guid, Item>>.Failure(
                StockTransferErrors.DuplicateItem);
        }

        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0 ||
                line.Quantity > MaxQuantity ||
                decimal.Round(line.Quantity, 3) != line.Quantity)
            {
                return Result<Dictionary<Guid, Item>>.Failure(
                    StockTransferErrors.InvalidQuantity);
            }
        }

        // Always lock warehouses in the same order, regardless of direction.
        // This prevents opposite transfers from locking them in reverse order.
        var warehouseIds = new[]
        {
            request.SourceWarehouseId,
            request.DestinationWarehouseId
        };

        foreach (var warehouseId in warehouseIds.OrderBy(id => id))
        {
            var matches = await _db.Warehouses
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM "Warehouses"
                    WHERE "Id" = {warehouseId}
                      AND NOT "IsDeleted"
                      AND "IsActive"
                    FOR UPDATE
                    """)
                .IgnoreQueryFilters()
                .AsNoTracking()
                .ToListAsync(ct);

            if (matches.Count == 0)
            {
                return Result<Dictionary<Guid, Item>>.Failure(
                    StockTransferErrors.WarehouseUnavailable);
            }
        }

        // Acquire item locks after both warehouse locks.
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
                    StockTransferErrors.ItemUnavailable);
            }

            items.Add(item.Id, item);
        }

        return Result<Dictionary<Guid, Item>>.Success(items);
    }

    private void ApplyDraft(
        StockTransfer transfer,
        SaveStockTransferRequest request,
        Dictionary<Guid, Item> items)
    {
        transfer.SourceWarehouseId = request.SourceWarehouseId;
        transfer.DestinationWarehouseId = request.DestinationWarehouseId;
        transfer.TransferDate = request.TransferDate;
        transfer.Notes = string.IsNullOrWhiteSpace(request.Notes)
            ? null
            : request.Notes.Trim();

        var requestedIds = request.Lines
            .Select(line => line.ItemId)
            .ToHashSet();

        foreach (var line in transfer.Lines
            .Where(line => !requestedIds.Contains(line.ItemId))
            .ToList())
        {
            transfer.Lines.Remove(line);
            _db.StockTransferLines.Remove(line);
        }

        var existingLines = transfer.Lines
            .ToDictionary(line => line.ItemId);

        foreach (var input in request.Lines)
        {
            if (!existingLines.TryGetValue(input.ItemId, out var line))
            {
                line = new StockTransferLine
                {
                    Id = Guid.NewGuid(),
                    StockTransferId = transfer.Id,
                    StockTransfer = transfer,
                    ItemId = input.ItemId
                };

                transfer.Lines.Add(line);
                _db.StockTransferLines.Add(line);
            }

            var item = items[input.ItemId];

            line.SKU = item.SKU;
            line.ItemName = item.Name;
            line.UnitOfMeasure = item.UnitOfMeasure;
            line.Quantity = input.Quantity;
        }
    }

    public static StockTransferResponse ToResponse(
        StockTransfer transfer) => new(
        transfer.Id,
        transfer.TransferNumber,
        transfer.SourceWarehouseId,
        transfer.DestinationWarehouseId,
        transfer.TransferDate,
        transfer.Notes,
        transfer.Status,
        transfer.PostedAt,
        transfer.CreatedAt,
        transfer.Lines
            .OrderBy(line => line.ItemName)
            .ThenBy(line => line.Id)
            .Select(line => new StockTransferLineResponse(
                line.Id,
                line.ItemId,
                line.SKU,
                line.ItemName,
                line.UnitOfMeasure,
                line.Quantity))
            .ToList());
}