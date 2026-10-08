using WebProveedores.Application.Auth.Commands;
using WebProveedores.Application.Auth.Responses;

namespace WebProveedores.Application.Auth;

public interface IProviderRegistrationService
{
    Task<ProviderLookupResponse> ValidateRucAsync(string ruc, CancellationToken cancellationToken);
    Task<AccessKeyResponse> RequestAccessKeyAsync(string ruc, CancellationToken cancellationToken);
    /// <summary>Alta directa con contraseña (solo administrador).</summary>
    Task<UserResponse> RegisterAsync(RegisterProviderCommand request, CancellationToken cancellationToken);
}
