using System.Security.Cryptography;
using System.Text;
using WebProveedores.Application.Abstractions.Auth;
using WebProveedores.Domain.Identity;

namespace WebProveedores.Application.Auth;

/// <summary>Piezas comunes a login, contraseñas y registro.</summary>
internal static class AuthSupport
{
    public static string PrimaryEmail(AppUser user) => user.PrimaryEmail;

    public static UserResponse ToResponse(AppUser user)
    {
        var roles = user.UserRoles.Where(item => item.Role.IsActive).Select(item => item.Role.Name).OrderBy(name => name).ToArray();
        return new(user.Id, user.Username, PrimaryEmail(user), user.CompanyName, user.Ruc ?? string.Empty, user.Area?.Name, roles.FirstOrDefault() ?? string.Empty, roles, user.MustChangePassword);
    }

    /// <summary>Sesión nueva: con contraseña temporal solo sirve para cambiarla.</summary>
    public static AuthResponse StartSession(this ITokenIssuer tokens, AppUser user)
    {
        var token = tokens.Issue(user, passwordChangeOnly: user.MustChangePassword);
        return new AuthResponse(token.Value, token.ExpiresAtUtc, ToResponse(user));
    }

    /// <summary>Token aleatorio de un solo uso para enlaces de correo (URL-safe).</summary>
    public static string NewOneTimeToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    /// <summary>En la base solo se guarda el hash del token.</summary>
    public static string HashOneTimeToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
