using WebProveedores.Application.Auth.Commands;
using WebProveedores.Application.Auth.Responses;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Application.Ports.Outbound.Persistence;
using WebProveedores.Application.Ports.Outbound.Security;
using WebProveedores.Domain.Identity;

namespace WebProveedores.Application.Auth;

/// <summary>Cambio de contraseña con sesión, recuperación por correo y confirmación por enlace (activación o recuperación).</summary>
internal sealed class PasswordService(
    IUserRepository users,
    IPasswordTokenRepository passwordTokens,
    IUnitOfWork unitOfWork,
    IPasswordHasher hasher,
    ITokenIssuer tokens,
    PasswordLinks links,
    TimeProvider clock) : IPasswordService
{
    public async Task<AuthResponse> ChangePasswordAsync(Guid userId, bool passwordChangeSession, ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await users.FindTrackedByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new ForbiddenException("La sesión no es válida.");

        // La sesión de cambio forzado se abrió con la contraseña temporal: no se vuelve a pedir.
        var forcedChange = user.MustChangePassword && passwordChangeSession;
        if (!forcedChange && (string.IsNullOrEmpty(request.CurrentPassword) || !hasher.Verify(user.PasswordHash, request.CurrentPassword)))
            throw new ValidationException("La contraseña actual no es correcta.");
        if (hasher.Verify(user.PasswordHash, request.NewPassword))
            throw new ValidationException("La nueva contraseña debe ser distinta de la actual.");
        if (!PasswordPolicy.IsSatisfiedBy(request.NewPassword))
            throw new ValidationException(PasswordPolicy.Description);

        user.SetPassword(hasher.Hash(request.NewPassword), clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return tokens.StartSession(user);
    }

    public async Task<PasswordResetResponse?> RequestPasswordResetAsync(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var user = await users.FindByRucAsync(request.Ruc.Trim(), cancellationToken);
        var email = user is null ? null : AuthSupport.PrimaryEmail(user);
        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(email)) return null;

        await links.SendAsync(user, PasswordTokenPurpose.PasswordReset, cancellationToken);
        return new PasswordResetResponse(true, MaskEmail(email));
    }

    public async Task<bool> ConfirmPasswordResetAsync(ConfirmPasswordResetCommand request, PasswordTokenPurpose purpose, CancellationToken cancellationToken)
    {
        if (!PasswordPolicy.IsSatisfiedBy(request.NewPassword))
            throw new ValidationException(PasswordPolicy.Description);
        var ruc = request.Ruc?.Trim();
        var username = request.User?.Trim();
        if (string.IsNullOrEmpty(ruc) && string.IsNullOrEmpty(username)) return false;
        var resetToken = await passwordTokens.FindValidAsync(AuthSupport.HashOneTimeToken(request.Token), purpose, cancellationToken);
        // El enlace trae la cuenta a la que se emitió: debe coincidir.
        if (resetToken is null || (!string.IsNullOrEmpty(ruc) && resetToken.User.Ruc != ruc)
            || (!string.IsNullOrEmpty(username) && !string.Equals(resetToken.User.Username, username, StringComparison.OrdinalIgnoreCase)))
            return false;

        var now = clock.GetUtcNow().UtcDateTime;
        resetToken.User.SetPassword(hasher.Hash(request.NewPassword), now);
        if (purpose == PasswordTokenPurpose.Activation) resetToken.User.ConfirmPrimaryEmail(now);
        resetToken.UsedAtUtc = now;
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
