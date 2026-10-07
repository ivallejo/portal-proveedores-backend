using WebProveedores.Application.Admin;

namespace WebProveedores.Application.Ports.Inbound.Admin;

public interface IAdminUserService
{
    Task<AdminUserPage> SearchAsync(string? search, string? role, string? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<AdminCatalogResponse> CatalogAsync(CancellationToken cancellationToken);
    Task<AdminUserDetail?> GetAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>Crea la cuenta y envía el enlace de activación al correo principal.</summary>
    Task<AdminUserDetail> CreateAsync(SaveUserRequest request, CancellationToken cancellationToken);
    Task<AdminUserDetail?> UpdateAsync(Guid actorId, Guid id, SaveUserRequest request, CancellationToken cancellationToken);
    /// <summary>Activar también quita el bloqueo por intentos fallidos.</summary>
    Task<AdminUserDetail?> SetStatusAsync(Guid actorId, Guid id, UpdateUserStatusRequest request, CancellationToken cancellationToken);
    /// <summary>Activación si aún no definió su contraseña; si no, recuperación.</summary>
    Task<PasswordLinkSent?> SendPasswordLinkAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<PasswordLinkResponse>?> PasswordLinksAsync(Guid id, CancellationToken cancellationToken);
}
