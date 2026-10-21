using System.Security.Cryptography;
using System.Text;
using WebProveedores.Application.Auth.Responses;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Application.Ports.Outbound.Security;
using WebProveedores.Domain.Identity;

namespace WebProveedores.Application.Auth;

/// <summary>Piezas comunes a login, contraseñas y registro.</summary>
internal static class AuthSupport
{
    public static string PrimaryEmail(AppUser user) => user.PrimaryEmail;

    public static UserResponse ToResponse(AppUser user, DateTime now)
    {
        var roles = user.UserRoles.Where(item => item.Role.IsActive).Select(item => item.Role.Name).OrderBy(name => name).ToArray();
        return new(user.Id, user.Username, PrimaryEmail(user), user.CompanyName, user.Ruc ?? string.Empty, user.Area?.Name, roles.FirstOrDefault() ?? string.Empty, roles,
            user.MustChangePasswordAt(now), !user.MustChangePassword && user.IsPasswordExpired(now));
    }

    /// <summary>Sesión nueva: con contraseña temporal o vencida solo sirve para cambiarla.</summary>
    public static AuthResponse StartSession(this ITokenIssuer tokens, AppUser user, DateTime now)
    {
        var token = tokens.Issue(user, passwordChangeOnly: user.MustChangePasswordAt(now));
        return new AuthResponse(token.Value, token.ExpiresAtUtc, ToResponse(user, now));
    }

    /// <summary>Aplica la política (<see cref="PasswordPolicy"/>) y el historial: la nueva no puede repetir las últimas.</summary>
    public static void EnsureValidNewPassword(AppUser user, string password, IPasswordHasher hasher)
    {
        if (user.RecentPasswordHashes.Any(hash => hasher.Verify(hash, password)))
            throw new ValidationException(PasswordPolicy.Reused);
        var violations = PasswordPolicy.Violations(password, user.PasswordPersonalData);
        if (violations.Count > 0) throw new ValidationException(string.Join(" ", violations));
    }

    /// <summary>Token aleatorio de un solo uso para enlaces de correo (URL-safe).</summary>
    public static string NewOneTimeToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    /// <summary>En la base solo se guarda el hash del token.</summary>
    public static string HashOneTimeToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
