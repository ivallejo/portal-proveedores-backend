using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using WebProveedores.Application.Common.Security;
using WebProveedores.Application.Ports.Outbound.Security;
using WebProveedores.Application.Ports.Outbound.Security.Models;
using WebProveedores.Domain.Identity;

namespace WebProveedores.Infrastructure.Security;

public sealed class JwtTokenIssuer(JwtSettings settings, TimeProvider clock) : ITokenIssuer
{
    public IssuedToken Issue(AppUser user, bool passwordChangeOnly)
    {
        var expires = clock.GetUtcNow().UtcDateTime.AddMinutes(settings.AccessTokenMinutes);
        var email = user.Emails.FirstOrDefault(item => item.IsPrimary && item.IsActive)?.Email ?? user.Emails.FirstOrDefault(item => item.IsActive)?.Email ?? string.Empty;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.Email, email),
            new(SessionClaims.Username, user.Username),
            new(SessionClaims.Ruc, user.Ruc ?? string.Empty),
        };
        // Con la contraseña temporal solo se permite cambiarla: la API lo exige leyendo este claim.
        if (passwordChangeOnly) claims.Add(new Claim(SessionClaims.PasswordChangeOnly, "1"));
        claims.AddRange(user.UserRoles.Where(item => item.Role.IsActive).Select(item => new Claim(ClaimTypes.Role, item.Role.Code)));

        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, expires: expires, signingCredentials: credentials);
        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
