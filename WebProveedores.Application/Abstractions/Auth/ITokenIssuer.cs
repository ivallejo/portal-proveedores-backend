using WebProveedores.Domain.Identity;

namespace WebProveedores.Application.Abstractions.Auth;

/// <summary>Emite el token de sesión de un usuario autenticado (hoy un JWT).</summary>
public interface ITokenIssuer
{
    /// <param name="passwordChangeOnly">La sesión solo sirve para cambiar la contraseña temporal.</param>
    IssuedToken Issue(AppUser user, bool passwordChangeOnly);
}

public sealed record IssuedToken(string Value, DateTime ExpiresAtUtc);
