using Stackline.API.Common;
using Stackline.API.Features.SupplierPayments.Dtos;

namespace Stackline.API.Features.SupplierPayments;

public interface ISupplierPaymentService
{
    Task<Result<SupplierPaymentResponse>> CreateAsync(
        CreateSupplierPaymentRequest request,
        CancellationToken ct);

    Task<Result<SupplierPaymentResponse>> UpdateAsync(
        Guid id,
        UpdateSupplierPaymentRequest request,
        CancellationToken ct);

    Task<Result<Guid>> DeleteAsync(
        Guid id,
        CancellationToken ct);

    Task<Result<SupplierPaymentResponse>> PostAsync(
        Guid id,
        CancellationToken ct);
}