using WebProveedores.Application.Abstractions.Auth;
using WebProveedores.Application.Abstractions.Persistence;
using WebProveedores.Domain.Entities;

namespace WebProveedores.Application.Auth;

/// <summary>Cambio de contraseña con sesión, recuperación por correo y confirmación por enlace (activación o recuperación).</summary>
public sealed class PasswordService(
    IUserRepository users,
    IPasswordTokenRepository passwordTokens,
    IUnitOfWork unitOfWork,
    IPasswordHasher hasher,
    ITokenIssuer tokens,
    IEmailSender emailSender,
    PortalSettings portal,
    TimeProvider clock) : IPasswordService
{
    public async Task<AuthResponse> ChangePasswordAsync(Guid userId, bool passwordChangeSession, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindTrackedByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAccessException("La sesión no es válida.");

        // La sesión de cambio forzado se abrió con la contraseña temporal: no se vuelve a pedir.
        var forcedChange = user.MustChangePassword && passwordChangeSession;
        if (!forcedChange && (string.IsNullOrEmpty(request.CurrentPassword) || !hasher.Verify(user.PasswordHash, request.CurrentPassword)))
            throw new ArgumentException("La contraseña actual no es correcta.");
        if (hasher.Verify(user.PasswordHash, request.NewPassword))
            throw new ArgumentException("La nueva contraseña debe ser distinta de la actual.");
        if (!PasswordPolicy.IsSatisfiedBy(request.NewPassword))
            throw new ArgumentException(PasswordPolicy.Description);

        var now = clock.GetUtcNow().UtcDateTime;
        user.PasswordHash = hasher.Hash(request.NewPassword);
        user.MustChangePassword = false;
        user.PasswordSetAtUtc = now;
        user.UpdatedAtUtc = now;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return tokens.StartSession(user);
    }

    public async Task<PasswordResetResponse?> RequestPasswordResetAsync(PasswordResetRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByRucAsync(request.Ruc.Trim(), cancellationToken);
        var email = user is null ? null : AuthSupport.PrimaryEmail(user);
        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(email)) return null;

        var token = AuthSupport.NewOneTimeToken();
        passwordTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = AuthSupport.HashOneTimeToken(token),
            Purpose = PasswordTokenPurpose.PasswordReset,
            ExpiresAtUtc = clock.GetUtcNow().UtcDateTime.AddHours(24),
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var resetUrl = portal.Link($"ruc={Uri.EscapeDataString(user.Ruc!)}&resetToken={Uri.EscapeDataString(token)}");
        await emailSender.SendAsync(email, "Cambia tu contraseña - Portal de Proveedores", EmailTemplates.PasswordReset(user.CompanyName, resetUrl), cancellationToken, isHtml: true);
        return new PasswordResetResponse(true, MaskEmail(email));
    }

    public async Task<bool> ConfirmPasswordResetAsync(PasswordResetConfirmRequest request, PasswordTokenPurpose purpose, CancellationToken cancellationToken)
    {
        if (!PasswordPolicy.IsSatisfiedBy(request.NewPassword))
            throw new ArgumentException(PasswordPolicy.Description);
        var resetToken = await passwordTokens.FindValidAsync(request.Ruc.Trim(), AuthSupport.HashOneTimeToken(request.Token), purpose, cancellationToken);
        if (resetToken is null) return false;

        var now = clock.GetUtcNow().UtcDateTime;
        resetToken.User.PasswordHash = hasher.Hash(request.NewPassword);
        resetToken.UsedAtUtc = now;
        resetToken.User.UpdatedAtUtc = now;
        resetToken.User.PasswordSetAtUtc = now;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string MaskEmail(string email)
    {
        var parts = email.Split('@', 2);
        if (parts.Length != 2) return email;
        return $"{parts[0][..Math.Min(3, parts[0].Length)]}*****{parts[1]}";
    }
}
