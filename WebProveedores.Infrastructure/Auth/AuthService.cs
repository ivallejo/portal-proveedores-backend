using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using WebProveedores.Application.Auth;
using WebProveedores.Domain.Entities;
using WebProveedores.Infrastructure.Persistence;

namespace WebProveedores.Infrastructure.Auth;

public sealed class AuthService(AppDbContext db, IConfiguration configuration, IEmailSender emailSender) : IAuthService
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

    public async Task<PasswordResetResponse?> RequestPasswordResetAsync(PasswordResetRequest request, CancellationToken cancellationToken)
    {
        var ruc = request.Ruc.Trim();
        var user = await db.Users.SingleOrDefaultAsync(item => item.Ruc == ruc, cancellationToken);
        if (user is null || !user.IsActive) return null;

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        user.PasswordResetTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        user.PasswordResetTokenExpiresAtUtc = DateTime.UtcNow.AddHours(24);
        await db.SaveChangesAsync(cancellationToken);
        await emailSender.SendAsync(user.Email, "Recuperación de contraseña - Portal de Proveedores", $"Usa este token para cambiar tu contraseña: {token}", cancellationToken);
        return new PasswordResetResponse(true, MaskEmail(user.Email), token);
    }

    public async Task<bool> ConfirmPasswordResetAsync(PasswordResetConfirmRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(item => item.Ruc == request.Ruc.Trim(), cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(user.PasswordResetTokenHash) || user.PasswordResetTokenExpiresAtUtc <= DateTime.UtcNow) return false;
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Token)));
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(user.PasswordResetTokenHash), Encoding.UTF8.GetBytes(tokenHash))) return false;
        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpiresAtUtc = null;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public UserResponse? GetCurrentUser(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return id is not null && Guid.TryParse(id, out var userId) ? db.Users.AsNoTracking().Where(user => user.Id == userId).Select(user => new UserResponse(user.Id, user.Username ?? user.Ruc, user.Email, user.CompanyName, user.Ruc, user.Area, user.Role, new[] { user.Role })).SingleOrDefault() : null;
    }

    private static UserResponse ToResponse(AppUser user) => new(user.Id, user.Username ?? user.Ruc, user.Email, user.CompanyName, user.Ruc, user.Area, user.Role, RolesFor(user));
    private static IReadOnlyList<string> RolesFor(AppUser user) => [user.Role];
    private static string MaskEmail(string email)
    {
        var parts = email.Split('@', 2);
        if (parts.Length != 2) return email;
        var local = parts[0];
        return $"{local[..Math.Min(3, local.Length)]}*****{parts[1]}";
    }
}
