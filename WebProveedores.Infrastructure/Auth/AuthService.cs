using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using WebProveedores.Application.Auth;
using WebProveedores.Application.Abstractions.Auth;
using WebProveedores.Application.Abstractions.Persistence;
using WebProveedores.Domain.Entities;
using WebProveedores.Infrastructure.Persistence;

namespace WebProveedores.Infrastructure.Auth;

public sealed class AuthService(IAppDbContext db, IConfiguration configuration, IEmailSender emailSender) : IAuthService
{
    private readonly PasswordHasher<AppUser> passwordHasher = new();

    public async Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var ruc = request.Ruc.Trim();
        if (await db.UserEmails.AnyAsync(item => item.Email == email, cancellationToken) || await db.Users.AnyAsync(item => item.Ruc == ruc, cancellationToken))
            throw new InvalidOperationException("Ya existe un usuario registrado con ese correo o RUC.");

        var user = new AppUser { Username = ruc, CompanyName = request.CompanyName.Trim(), Ruc = ruc, PasswordSetAtUtc = DateTime.UtcNow };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        user.Emails.Add(new UserEmail { Email = email, IsPrimary = true });
        user.UserRoles.Add(new UserRole { Role = await GetRoleAsync(SecurityCatalog.ProviderRole, cancellationToken) });
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var identifier = request.Identifier.Trim();
        var normalizedEmail = identifier.ToLowerInvariant();
        var user = await db.Users
            .Include(item => item.Emails)
            .Include(item => item.Area)
            .Include(item => item.UserRoles).ThenInclude(item => item.Role)
            .SingleOrDefaultAsync(item => item.Username == identifier || item.Ruc == identifier || item.Emails.Any(email => email.Email == normalizedEmail), cancellationToken);
        if (user is null || !user.IsActive || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed) return null;

        var expires = DateTime.UtcNow.AddMinutes(configuration.GetValue("Jwt:AccessTokenMinutes", 30));
        var key = configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey no está configurado.");
        var roles = RolesFor(user);
        var primaryEmail = PrimaryEmail(user);
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new(ClaimTypes.Email, primaryEmail), new("username", user.Username), new("ruc", user.Ruc ?? string.Empty) };
        claims.AddRange(user.UserRoles.Where(item => item.Role.IsActive).Select(item => new Claim(ClaimTypes.Role, item.Role.Code)));
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"], claims, expires: expires, signingCredentials: credentials);
        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), expires, ToResponse(user));
    }

    public async Task<PasswordResetResponse?> RequestPasswordResetAsync(PasswordResetRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.Include(item => item.Emails).SingleOrDefaultAsync(item => item.Ruc == request.Ruc.Trim(), cancellationToken);
        var email = user is null ? null : PrimaryEmail(user);
        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(email)) return null;

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
            Purpose = PasswordTokenPurpose.PasswordReset,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(24),
        });
        await db.SaveChangesAsync(cancellationToken);
        var frontendUrl = configuration["Frontend:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:4200";
        var resetUrl = $"{frontendUrl}/?ruc={Uri.EscapeDataString(user.Ruc!)}&resetToken={Uri.EscapeDataString(token)}";
        await emailSender.SendAsync(email, "Cambia tu contraseña - Portal de Proveedores", EmailTemplates.PasswordReset(user.CompanyName, resetUrl), cancellationToken, isHtml: true);
        return new PasswordResetResponse(true, MaskEmail(email));
    }

    public async Task<bool> ConfirmPasswordResetAsync(PasswordResetConfirmRequest request, PasswordTokenPurpose purpose, CancellationToken cancellationToken)
    {
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Token)));
        var resetToken = await db.PasswordResetTokens.Include(item => item.User).ThenInclude(item => item.Emails)
            .SingleOrDefaultAsync(item => item.User.Ruc == request.Ruc.Trim() && item.TokenHash == tokenHash && item.Purpose == purpose && item.UsedAtUtc == null && item.ExpiresAtUtc > DateTime.UtcNow, cancellationToken);
        if (resetToken is null) return false;

        resetToken.User.PasswordHash = passwordHasher.HashPassword(resetToken.User, request.NewPassword);
        resetToken.UsedAtUtc = DateTime.UtcNow;
        resetToken.User.UpdatedAtUtc = DateTime.UtcNow;
        resetToken.User.PasswordSetAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<UserResponse?> GetCurrentUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var id = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id is null || !Guid.TryParse(id, out var userId)) return null;
        var user = await db.Users.AsNoTracking().Include(item => item.Emails).Include(item => item.Area).Include(item => item.UserRoles).ThenInclude(item => item.Role).SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        return user is null ? null : ToResponse(user);
    }

    private async Task<Role> GetRoleAsync(string code, CancellationToken cancellationToken) => await db.Roles.SingleAsync(role => role.Code == code, cancellationToken);
    private static string PrimaryEmail(AppUser user) => user.Emails.FirstOrDefault(item => item.IsPrimary && item.IsActive)?.Email ?? user.Emails.First(item => item.IsActive).Email;
    private static UserResponse ToResponse(AppUser user) { var roles = RolesFor(user); return new(user.Id, user.Username, PrimaryEmail(user), user.CompanyName, user.Ruc ?? string.Empty, user.Area?.Name, roles.FirstOrDefault() ?? string.Empty, roles); }
    private static IReadOnlyList<string> RolesFor(AppUser user) => user.UserRoles.Where(item => item.Role.IsActive).Select(item => item.Role.Name).OrderBy(name => name).ToArray();
    private static string MaskEmail(string email) { var parts = email.Split('@', 2); if (parts.Length != 2) return email; var local = parts[0]; return $"{local[..Math.Min(3, local.Length)]}*****{parts[1]}"; }
}
