using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using WebProveedores.Application.Auth;
using WebProveedores.Domain.Entities;
using WebProveedores.Infrastructure.Persistence;

namespace WebProveedores.Infrastructure.Auth;

public sealed class AuthService(AppDbContext db, IConfiguration configuration) : IAuthService
{
    private readonly PasswordHasher<AppUser> passwordHasher = new();

    public async Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(user => user.Email == email || user.Ruc == request.Ruc.Trim(), cancellationToken))
            throw new InvalidOperationException("Ya existe un usuario registrado con ese correo o RUC.");

        var ruc = request.Ruc.Trim();
        var user = new AppUser { Username = ruc, Email = email, CompanyName = request.CompanyName.Trim(), Ruc = ruc, Role = "Proveedor" };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var identifier = request.Identifier.Trim();
        var normalizedEmail = identifier.ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(
            item => item.Email == normalizedEmail || item.Ruc == identifier || item.Username == identifier,
            cancellationToken);
        if (user is null || !user.IsActive || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed) return null;

        var expires = DateTime.UtcNow.AddMinutes(configuration.GetValue("Jwt:AccessTokenMinutes", 30));
        var key = configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey no está configurado.");
        var roles = RolesFor(user);
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new(ClaimTypes.Email, user.Email), new("username", user.Username ?? user.Ruc), new("ruc", user.Ruc) };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"], claims, expires: expires, signingCredentials: credentials);
        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), expires, ToResponse(user));
    }

    public UserResponse? GetCurrentUser(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return id is not null && Guid.TryParse(id, out var userId) ? db.Users.AsNoTracking().Where(user => user.Id == userId).Select(user => new UserResponse(user.Id, user.Username ?? user.Ruc, user.Email, user.CompanyName, user.Ruc, user.Area, user.Role, new[] { user.Role })).SingleOrDefault() : null;
    }

    private static UserResponse ToResponse(AppUser user) => new(user.Id, user.Username ?? user.Ruc, user.Email, user.CompanyName, user.Ruc, user.Area, user.Role, RolesFor(user));
    private static IReadOnlyList<string> RolesFor(AppUser user) => [user.Role];
}
