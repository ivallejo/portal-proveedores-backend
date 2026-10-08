using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Application.Common.Security;

namespace WebProveedores.Api.Security;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal Principal => accessor.HttpContext?.User ?? new ClaimsPrincipal();

    public Guid Id =>
        Guid.TryParse(Principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? Principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
            ? id
            : throw new ForbiddenException("La sesión no es válida.");

    public bool IsPasswordChangeSession => Principal.HasClaim(SessionClaims.PasswordChangeOnly, "1");
}
