namespace Stackline.API.Features.Auth.Dtos;

public record LoginRequest(string Email, string Password);
public record LoginResponse(string Token, string Role, Guid? TenantId, string FullName);