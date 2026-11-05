using WebProveedores.Application.Ports.Outbound.Security.Models;
using WebProveedores.Domain.Identity;

namespace WebProveedores.Application.Ports.Outbound.Security;

/// <summary>Emite el token de sesión de un usuario autenticado (hoy un JWT).</summary>
public interface ITokenIssuer
{
    /// <param name="passwordChangeOnly">La sesión solo sirve para cambiar la contraseña temporal.</param>
    /// <param name="sessionStartedAtUtc">Cuándo se ingresó; al renovar se conserva el del token anterior.</param>
    IssuedToken Issue(AppUser user, bool passwordChangeOnly, DateTime sessionStartedAtUtc);
}
