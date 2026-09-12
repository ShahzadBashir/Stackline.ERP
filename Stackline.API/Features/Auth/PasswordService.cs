using Microsoft.AspNetCore.Identity;
using Stackline.API.Data.Entities;

namespace Stackline.API.Features.Auth;

public interface IPasswordService
{
    string HashPassword(string plainPassword);
    bool VerifyPassword(string plainPassword, string hashPassword);
}

public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<GlobalUser> _hasher = new();
    public string HashPassword(string plainPassword)
    {
        return _hasher.HashPassword(null!, plainPassword);
    }

    public bool VerifyPassword(string plainPassword, string hashPassword)
    {
        var result = _hasher.VerifyHashedPassword(null!, hashPassword, plainPassword);
        return result is PasswordVerificationResult.Success || result is PasswordVerificationResult.SuccessRehashNeeded;
    }
}
