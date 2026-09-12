using Microsoft.EntityFrameworkCore;
using Stackline.API.Data;
using Stackline.API.Features.Auth.Dtos;

namespace Stackline.API.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", async (
            LoginRequest request,
            MasterDbContext db,
            IPasswordService passwordService,
            IJwtTokenService tokenService,
            CancellationToken ct) =>
        {
            var user = await db.GlobalUsers
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive, ct);

            if (user is null || !passwordService.VerifyPassword(request.Password, user.PasswordHash))
                return Results.Unauthorized();

            if (user.TenantId.HasValue)
            {
                var tenant = await db.Tenants.FirstOrDefaultAsync(x=>x.Id == user.TenantId.Value, ct);
                if (tenant is null || !tenant.IsActive)
                    return Results.Json(new { message = "This account is suspended." }, statusCode: 403);
            }

            var token = tokenService.GenerateToken(user);
            return Results.Ok(new LoginResponse(token, user.Role, user.TenantId, user.FullName));
        })
        .WithName("Login")
        .AllowAnonymous();

        return app;
    }
}