using WebProveedores.Application.Profile;

namespace WebProveedores.Application.Ports.Inbound.Profile;

/// <summary>Mi perfil: datos, correos (con verificación) de quien tiene sesión.</summary>
public interface IProfileService
{
    Task<ProfileResponse> GetAsync(Guid userId, CancellationToken cancellationToken);
    Task<ProfileResponse> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken);
    Task<ProfileResponse> AddEmailAsync(Guid userId, AddEmailRequest request, CancellationToken cancellationToken);
    Task<ProfileResponse> ResendVerificationAsync(Guid userId, Guid emailId, CancellationToken cancellationToken);
    Task<ProfileResponse> MakePrimaryAsync(Guid userId, Guid emailId, CancellationToken cancellationToken);
    Task<ProfileResponse> RemoveEmailAsync(Guid userId, Guid emailId, CancellationToken cancellationToken);
    /// <summary>Enlace del correo de verificación (sin sesión). Devuelve el correo verificado o null si el enlace no sirve.</summary>
    Task<string?> VerifyEmailAsync(string token, CancellationToken cancellationToken);
}
