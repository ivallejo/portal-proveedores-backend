using WebProveedores.Application.Auth;

namespace WebProveedores.Application.Ports.Inbound.Auth;

public interface ILoginService
{
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<UserResponse?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);
}
