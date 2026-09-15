using Stackline.API.Common;
using Stackline.API.Features.Suppliers.Dtos;

namespace Stackline.API.Features.Suppliers;

public interface ISupplierBalanceService
{
    Task<Result<SupplierBalanceResponse>> GetAsync(
        Guid supplierId,
        CancellationToken ct);
}