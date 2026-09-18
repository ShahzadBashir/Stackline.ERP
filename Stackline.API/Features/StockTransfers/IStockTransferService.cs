using Stackline.API.Common;
using Stackline.API.Features.StockTransfers.Dtos;

namespace Stackline.API.Features.StockTransfers;

public interface IStockTransferService
{
    Task<Result<StockTransferResponse>> CreateAsync(
        SaveStockTransferRequest request,
        CancellationToken ct);

    Task<Result<StockTransferResponse>> UpdateAsync(
        Guid id,
        SaveStockTransferRequest request,
        CancellationToken ct);

    Task<Result<StockTransferResponse>> PostAsync(
        Guid id,
        CancellationToken ct);

    Task<Result<Guid>> DeleteAsync(
        Guid id,
        CancellationToken ct);
}