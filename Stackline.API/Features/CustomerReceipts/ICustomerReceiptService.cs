using Stackline.API.Common;
using Stackline.API.Features.CustomerReceipts.Dtos;

namespace Stackline.API.Features.CustomerReceipts;

public interface ICustomerReceiptService
{
    Task<Result<CustomerReceiptResponse>> CreateAsync(
        CreateCustomerReceiptRequest request,
        CancellationToken ct);

    Task<Result<CustomerReceiptResponse>> UpdateAsync(
        Guid id,
        UpdateCustomerReceiptRequest request,
        CancellationToken ct);

    Task<Result<Guid>> DeleteAsync(
        Guid id,
        CancellationToken ct);

    Task<Result<CustomerReceiptResponse>> PostAsync(
        Guid id,
        CancellationToken ct);
}