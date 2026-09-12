namespace Stackline.API.Features.Tenants.Dtos;

public record TenantResponse(Guid Id, string CompanyName, string? OwnerName, string? OwnerEmail, string SubscriptionStatus, bool IsActive, DateTime CreatedAt);
