using Stackline.API.Common;
using Stackline.API.Features.Statements.Dtos;

namespace Stackline.API.Features.Statements;

public interface IAccountStatementService
{
    Task<Result<AccountStatementResponse>> GetCustomerAsync(
        Guid customerId,
        DateOnly fromDate,
        DateOnly toDate,
        int page,
        int pageSize,
        CancellationToken ct);

    Task<Result<AccountStatementResponse>> GetSupplierAsync(
        Guid supplierId,
        DateOnly fromDate,
        DateOnly toDate,
        int page,
        int pageSize,
        CancellationToken ct);
}