using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Stackline.API.Features.Auth;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    string? Email { get; }
    string? Role { get; }
}

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User =>
        _httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var value = User?.FindFirstValue(
                ClaimTypes.NameIdentifier);

            return Guid.TryParse(value, out var id)
                ? id
                : null;
        }
    }

    public Guid? TenantId
    {
        get
        {
            var value = User?.FindFirstValue("tenant_id");

            return Guid.TryParse(value, out var id)
                ? id
                : null;
        }
    }

    public string? Email =>
        User?.FindFirstValue(ClaimTypes.Email);

    public string? Role =>
        User?.FindFirstValue(ClaimTypes.Role);
}