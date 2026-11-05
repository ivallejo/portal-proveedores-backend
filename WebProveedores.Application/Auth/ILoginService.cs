using WebProveedores.Application.Auth.Commands;
using WebProveedores.Application.Auth.Responses;

namespace WebProveedores.Application.Auth;

public interface ILoginService
{
    Task<AuthResponse?> LoginAsync(LoginCommand request, CancellationToken cancellationToken);
    Task<UserResponse?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Token nuevo para la misma sesión (se renueva mientras haya actividad). Vacío si la cuenta ya no existe, está inactiva
    /// o bloqueada, o si la sesión superó su tope de duración: hay que volver a ingresar.
    /// </summary>
    Task<AuthResponse?> RenewAsync(Guid userId, DateTime sessionStartedAtUtc, CancellationToken cancellationToken);
}
