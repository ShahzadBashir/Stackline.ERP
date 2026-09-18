using Stackline.API.Common;
using Stackline.API.Features.StockAdjustments.Dtos;

namespace Stackline.API.Features.StockAdjustments;

public interface IStockAdjustmentService
{
    Task<Result<StockAdjustmentResponse>> CreateAsync(
        SaveStockAdjustmentRequest request,
        CancellationToken ct);

    Task<Result<StockAdjustmentResponse>> UpdateAsync(
        Guid id,
        SaveStockAdjustmentRequest request,
        CancellationToken ct);

    Task<Result<StockAdjustmentResponse>> PostAsync(
        Guid id,
        CancellationToken ct);

    Task<Result<Guid>> DeleteAsync(
        Guid id,
        CancellationToken ct);
}