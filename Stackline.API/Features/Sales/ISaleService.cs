using Stackline.API.Common;
using Stackline.API.Features.Sales.Dtos;

namespace Stackline.API.Features.Sales;

public interface ISaleService
{
    Task<Result<SaleResponse>> CreateAsync(
        CreateSaleRequest request,
        CancellationToken ct);

    Task<Result<SaleResponse>> UpdateAsync(
        Guid id,
        UpdateSaleRequest request,
        CancellationToken ct);

    Task<Result<Guid>> DeleteAsync(
        Guid id,
        CancellationToken ct);

    Task<Result<SaleResponse>> PostAsync(
        Guid id,
        CancellationToken ct);
}