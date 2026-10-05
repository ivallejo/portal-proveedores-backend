using Microsoft.AspNetCore.Identity;
using WebProveedores.Application.Abstractions.Auth;

namespace WebProveedores.Infrastructure.Auth;

/// <summary>Adaptador sobre el hasher de ASP.NET Identity (PBKDF2); compatible con los hashes ya guardados.</summary>
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    // La implementación estándar no usa el usuario: basta un objeto cualquiera.
    private static readonly object AnyUser = new();
    private readonly PasswordHasher<object> hasher = new();

    public string Hash(string password) => hasher.HashPassword(AnyUser, password);

    public bool Verify(string hash, string password) =>
        hasher.VerifyHashedPassword(AnyUser, hash, password) != PasswordVerificationResult.Failed;
}
