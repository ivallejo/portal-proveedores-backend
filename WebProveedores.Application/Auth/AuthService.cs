using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using WebProveedores.Application.Abstractions.Auth;
using WebProveedores.Application.Abstractions.Persistence;
using WebProveedores.Domain.Entities;
namespace WebProveedores.Application.Auth;

public sealed class AuthService(IIdentityRepository db, IConfiguration configuration, IEmailSender emailSender) : IAuthService
{
    private static readonly AppUser DummyUser = new();
    private static readonly Lazy<string> DummyHash = new(() => new PasswordHasher<AppUser>().HashPassword(DummyUser, Guid.NewGuid().ToString("N")));

    private readonly PasswordHasher<AppUser> passwordHasher = new();

    public async Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var ruc = request.Ruc.Trim();
        if (await db.EmailExistsAsync(email, cancellationToken) || await db.UserExistsByRucAsync(ruc, cancellationToken))
            throw new InvalidOperationException("Ya existe un usuario registrado con ese correo o RUC.");

        var user = new AppUser { Username = ruc, CompanyName = request.CompanyName.Trim(), Ruc = ruc, PasswordSetAtUtc = DateTime.UtcNow };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        user.Emails.Add(new UserEmail { Email = email, IsPrimary = true });
        user.UserRoles.Add(new UserRole { Role = await db.FindRoleByCodeAsync(SecurityCatalog.ProviderRole, cancellationToken) ?? throw new InvalidOperationException("El rol de proveedor no está configurado.") });
        db.AddUser(user);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var identifier = request.Identifier.Trim();
        var normalizedEmail = identifier.ToLowerInvariant();
        var user = await db.FindForLoginAsync(identifier, normalizedEmail, cancellationToken);
        if (user is null || !user.IsActive)
        {
            // Se verifica igual un hash para que el tiempo de respuesta no delate si la cuenta existe.
            passwordHasher.VerifyHashedPassword(DummyUser, DummyHash.Value, request.Password);
            return null;
        }

        var now = DateTime.UtcNow;
        if (user.LockoutUntilUtc is { } lockedUntil && lockedUntil > now)
            throw new AccountLockedException(lockedUntil - now);

        if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= configuration.GetValue("Security:MaxFailedLogins", 5))
            {
                user.LockoutUntilUtc = now.AddMinutes(configuration.GetValue("Security:LockoutMinutes", 15));
                user.FailedLoginCount = 0;
            }
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }

        if (user.FailedLoginCount != 0 || user.LockoutUntilUtc is not null)
        {
            user.FailedLoginCount = 0;
            user.LockoutUntilUtc = null;
            await db.SaveChangesAsync(cancellationToken);
        }

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
        var user = await db.FindByRucAsync(request.Ruc.Trim(), cancellationToken);
        var email = user is null ? null : PrimaryEmail(user);
        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(email)) return null;

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        db.AddPasswordToken(new PasswordResetToken
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
        var resetToken = await db.FindValidTokenAsync(request.Ruc.Trim(), tokenHash, purpose, cancellationToken);
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
        var user = await db.FindByIdAsync(userId, cancellationToken);
        return user is null ? null : ToResponse(user);
    }

    private static string PrimaryEmail(AppUser user) => user.Emails.FirstOrDefault(item => item.IsPrimary && item.IsActive)?.Email ?? user.Emails.First(item => item.IsActive).Email;
    private static UserResponse ToResponse(AppUser user) { var roles = RolesFor(user); return new(user.Id, user.Username, PrimaryEmail(user), user.CompanyName, user.Ruc ?? string.Empty, user.Area?.Name, roles.FirstOrDefault() ?? string.Empty, roles); }
    private static IReadOnlyList<string> RolesFor(AppUser user) => user.UserRoles.Where(item => item.Role.IsActive).Select(item => item.Role.Name).OrderBy(name => name).ToArray();
    private static string MaskEmail(string email) { var parts = email.Split('@', 2); if (parts.Length != 2) return email; var local = parts[0]; return $"{local[..Math.Min(3, local.Length)]}*****{parts[1]}"; }
}
