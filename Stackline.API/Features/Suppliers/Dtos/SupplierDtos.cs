namespace Stackline.API.Features.Suppliers.Dtos;

public sealed record CreateSupplierRequest(
    string Name,
    string? ContactPerson,
    string? Phone,
    decimal OpeningBalance = 0);

public sealed record UpdateSupplierRequest(
    string Name,
    string? ContactPerson,
    string? Phone,
    decimal OpeningBalance,
    bool IsActive);

public sealed record SupplierResponse(
    Guid Id,
    string Name,
    string? ContactPerson,
    string? Phone,
    decimal OpeningBalance,
    bool IsActive,
    DateTime CreatedAt);