namespace Stackline.API.Tenants.Dtos;

public record TenantResponse(Guid Id, string CompanyName, string SubscriptionStatus, bool IsActive, DateTime CreatedAt);
