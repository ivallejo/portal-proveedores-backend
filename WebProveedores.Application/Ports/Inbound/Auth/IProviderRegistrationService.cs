using WebProveedores.Application.Auth;

namespace WebProveedores.Application.Ports.Inbound.Auth;

public interface IProviderRegistrationService
{
    Task<ProviderLookupResponse> ValidateRucAsync(string ruc, CancellationToken cancellationToken);
    Task<AccessKeyResponse> RequestAccessKeyAsync(string ruc, CancellationToken cancellationToken);
    /// <summary>Alta directa con contraseña (solo administrador).</summary>
    Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
}
