using Stackline.API.Common;
using Stackline.API.Features.Customers.Dtos;

namespace Stackline.API.Features.Customers;

public interface ICustomerBalanceService
{
    Task<Result<CustomerBalanceResponse>> GetAsync(
        Guid customerId,
        CancellationToken ct);
}