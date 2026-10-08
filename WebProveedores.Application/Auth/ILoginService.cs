using WebProveedores.Application.Auth.Commands;
using WebProveedores.Application.Auth.Responses;

namespace WebProveedores.Application.Auth;

public interface ILoginService
{
    Task<AuthResponse?> LoginAsync(LoginCommand request, CancellationToken cancellationToken);
    Task<UserResponse?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);
}
