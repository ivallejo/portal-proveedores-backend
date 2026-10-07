using WebProveedores.Application.Auth;
using WebProveedores.Domain.Identity;

namespace WebProveedores.Application.Ports.Inbound.Auth;

public interface IPasswordService
{
    /// <summary>
    /// Cambia la contraseña del usuario en sesión y devuelve una sesión nueva (sin la marca de cambio pendiente).
    /// <paramref name="passwordChangeSession"/>: la sesión se abrió con la contraseña temporal.
    /// </summary>
    Task<AuthResponse> ChangePasswordAsync(Guid userId, bool passwordChangeSession, ChangePasswordRequest request, CancellationToken cancellationToken);
    Task<PasswordResetResponse?> RequestPasswordResetAsync(PasswordResetRequest request, CancellationToken cancellationToken);
    Task<bool> ConfirmPasswordResetAsync(PasswordResetConfirmRequest request, PasswordTokenPurpose purpose, CancellationToken cancellationToken);
}
