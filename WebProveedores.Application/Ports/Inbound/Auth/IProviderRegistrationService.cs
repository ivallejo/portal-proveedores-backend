using WebProveedores.Application.Contracts.Auth.Requests;
using WebProveedores.Application.Contracts.Auth.Responses;

namespace WebProveedores.Application.Ports.Inbound.Auth;

public interface IProviderRegistrationService
{
    Task<ProviderLookupResponse> ValidateRucAsync(string ruc, CancellationToken cancellationToken);
    Task<AccessKeyResponse> RequestAccessKeyAsync(string ruc, CancellationToken cancellationToken);
    /// <summary>Alta directa con contraseña (solo administrador).</summary>
    Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
}
