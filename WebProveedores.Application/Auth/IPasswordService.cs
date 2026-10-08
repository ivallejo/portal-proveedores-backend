using WebProveedores.Application.Auth.Commands;
using WebProveedores.Application.Auth.Responses;
using WebProveedores.Domain.Identity;

namespace WebProveedores.Application.Auth;

public interface IPasswordService
{
    /// <summary>
    /// Cambia la contraseña del usuario en sesión y devuelve una sesión nueva (sin la marca de cambio pendiente).
    /// <paramref name="passwordChangeSession"/>: la sesión se abrió con la contraseña temporal.
    /// </summary>
    Task<AuthResponse> ChangePasswordAsync(Guid userId, bool passwordChangeSession, ChangePasswordCommand request, CancellationToken cancellationToken);
    Task<PasswordResetResponse?> RequestPasswordResetAsync(RequestPasswordResetCommand request, CancellationToken cancellationToken);
    Task<bool> ConfirmPasswordResetAsync(ConfirmPasswordResetCommand request, PasswordTokenPurpose purpose, CancellationToken cancellationToken);
}
