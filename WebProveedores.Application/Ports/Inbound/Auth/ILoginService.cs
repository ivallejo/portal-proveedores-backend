using WebProveedores.Application.Contracts.Auth.Commands;
using WebProveedores.Application.Contracts.Auth.Responses;

namespace WebProveedores.Application.Ports.Inbound.Auth;

public interface ILoginService
{
    Task<AuthResponse?> LoginAsync(LoginCommand request, CancellationToken cancellationToken);
    Task<UserResponse?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);
}
