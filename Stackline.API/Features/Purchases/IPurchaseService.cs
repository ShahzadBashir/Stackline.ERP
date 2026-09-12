using Stackline.API.Common;
using Stackline.API.Purchases.Dtos;

namespace Stackline.API.Features.Purchases;

public interface IPurchaseService
{
    Task<Result<PurchaseResponse>> CreateAsync(
        CreatePurchaseRequest request,
        CancellationToken ct);

    Task<Result<PurchaseResponse>> UpdateAsync(
        Guid id,
        UpdatePurchaseRequest request,
        CancellationToken ct);

    Task<Result<Guid>> DeleteAsync(
        Guid id,
        CancellationToken ct);

    Task<Result<PurchaseResponse>> PostAsync(
        Guid id,
        CancellationToken ct);
}