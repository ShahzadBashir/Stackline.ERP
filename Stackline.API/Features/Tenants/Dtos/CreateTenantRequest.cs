namespace Stackline.API.Features.Tenants.Dtos;

public sealed record CreateTenantRequest(
    string CompanyName,
    string OwnerFullName,
    string OwnerEmail,
    string OwnerPassword
);
