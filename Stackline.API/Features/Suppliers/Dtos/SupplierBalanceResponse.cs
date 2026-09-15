namespace Stackline.API.Features.Suppliers.Dtos;

public sealed record SupplierBalanceResponse(
    Guid SupplierId,
    string SupplierName,
    decimal OpeningBalance,
    decimal PostedPurchasesAmount,
    decimal PostedPaymentsAmount,
    decimal OutstandingBalance);