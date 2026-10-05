using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using WebProveedores.Infrastructure.Auth;

namespace WebProveedores.Api.Infrastructure;

/// <summary>Usuario de la petición actual, leído del token. Los controladores no leen claims directamente.</summary>
public interface ICurrentUser
{
    /// <summary>Id del usuario autenticado; si el token no lo trae, la sesión no es válida.</summary>
    Guid Id { get; }

    /// <summary>La sesión se abrió con una contraseña temporal y solo sirve para cambiarla.</summary>
    bool IsPasswordChangeSession { get; }
}

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal Principal => accessor.HttpContext?.User ?? new ClaimsPrincipal();

    public Guid Id =>
        Guid.TryParse(Principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? Principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
            ? id
            : throw new UnauthorizedAccessException("La sesión no es válida.");

    public bool IsPasswordChangeSession => Principal.HasClaim(SessionClaims.PasswordChangeOnly, "1");
}
