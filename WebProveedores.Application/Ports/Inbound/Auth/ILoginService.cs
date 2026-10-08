using WebProveedores.Application.Contracts.Auth.Requests;
using WebProveedores.Application.Contracts.Auth.Responses;

namespace WebProveedores.Application.Ports.Inbound.Auth;

public interface ILoginService
{
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<UserResponse?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);
}
