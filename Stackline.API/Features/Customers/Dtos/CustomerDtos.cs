namespace Stackline.API.Features.Customers.Dtos;

public sealed record CreateCustomerRequest(
    string Name,
    string? ContactPerson,
    string? Phone,
    decimal CreditLimit = 0,
    decimal OpeningBalance = 0);

public sealed record UpdateCustomerRequest(
    string Name,
    string? ContactPerson,
    string? Phone,
    decimal CreditLimit,
    decimal OpeningBalance,
    bool IsActive);

public sealed record CustomerResponse(
    Guid Id,
    string Name,
    string? ContactPerson,
    string? Phone,
    decimal CreditLimit,
    decimal OpeningBalance,
    bool IsActive,
    DateTime CreatedAt);