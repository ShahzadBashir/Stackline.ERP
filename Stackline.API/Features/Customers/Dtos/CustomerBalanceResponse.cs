namespace Stackline.API.Features.Customers.Dtos;

public sealed record CustomerBalanceResponse(
    Guid CustomerId,
    string CustomerName,
    decimal OpeningBalance,
    decimal PostedSalesUnpaidAmount,
    decimal PostedReceiptsAmount,
    decimal OutstandingBalance,
    decimal CreditLimit,
    decimal AvailableCredit);