namespace Stackline.API.Tenants.Dtos;

public sealed record CreateTenantRequest(
    string CompanyName,
    string OwnerFullName,
    string OwnerEmail,
    string OwnerPassword
);
