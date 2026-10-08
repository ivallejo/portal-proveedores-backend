using WebProveedores.Application.Ports.Outbound.Security.Models;
using WebProveedores.Domain.Identity;

namespace WebProveedores.Application.Ports.Outbound.Security;

/// <summary>Emite el token de sesión de un usuario autenticado (hoy un JWT).</summary>
public interface ITokenIssuer
{
    /// <param name="passwordChangeOnly">La sesión solo sirve para cambiar la contraseña temporal.</param>
    IssuedToken Issue(AppUser user, bool passwordChangeOnly);
}
