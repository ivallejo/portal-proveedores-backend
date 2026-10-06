using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Application.Admin;

public sealed class UserEmailInput
{
    /// <summary>Vacío para un correo nuevo.</summary>
    public Guid? Id { get; init; }
    [Required, MaxLength(320)] public string Email { get; init; } = string.Empty;
    /// <summary>work, billing o personal.</summary>
    public string Type { get; init; } = "work";
    public bool IsPrimary { get; init; }
}

/// <summary>
/// Alta y edición de un usuario (un solo rol). Proveedor: RUC y razón social. Personal interno: DNI, nombres,
/// apellidos y área. El documento es el usuario de acceso: solo se indica al crear.
/// </summary>
public sealed record SaveUserRequest
{
    [Required, MaxLength(40)] public string Role { get; init; } = string.Empty;
    [MaxLength(11)] public string? Document { get; init; }
    [MaxLength(200)] public string? BusinessName { get; init; }
    [MaxLength(100)] public string? FirstName { get; init; }
    [MaxLength(100)] public string? LastName { get; init; }
    public Guid? AreaId { get; init; }
    public IReadOnlyList<string> CompanyCodes { get; init; } = [];
    public IReadOnlyList<UserEmailInput> Emails { get; init; } = [];
    /// <summary>active o inactive. «locked» deja el bloqueo como está (solo si ya estaba bloqueado).</summary>
    public string Status { get; init; } = "active";
    /// <summary>Solicitar cambio de contraseña en el próximo inicio (solo al editar).</summary>
    public bool MustChangePassword { get; init; }
}

public sealed record UpdateUserStatusRequest(bool IsActive);

public sealed record AdminUserEmail(Guid Id, string Email, string Type, bool IsPrimary, bool IsVerified, DateTime CreatedAtUtc);

public sealed record AdminUserSummary(
    Guid Id,
    string DisplayName,
    string PrimaryEmail,
    bool IsProvider,
    string Document,
    string DocumentType,
    string? Role,
    string? RoleName,
    string? AreaName,
    IReadOnlyList<string> CompanyCodes,
    string Status,
    bool IsActivated);

public sealed record AdminUserDetail(
    Guid Id,
    string Username,
    bool IsProvider,
    string Document,
    string DocumentType,
    string DisplayName,
    string? BusinessName,
    string? FirstName,
    string? LastName,
    string? Role,
    Guid? AreaId,
    IReadOnlyList<string> CompanyCodes,
    IReadOnlyList<AdminUserEmail> Emails,
    string Status,
    bool IsActivated,
    bool MustChangePassword,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record AdminUserCounts(int Total, int Active, int BlockedOrInactive);

public sealed record AdminUserPage(IReadOnlyList<AdminUserSummary> Items, int Total, int Page, int PageSize, AdminUserCounts Counts);

public sealed record AdminRoleOption(string Code, string Name, string? Description, bool IsProvider, bool IsActive);

public sealed record AdminAreaOption(Guid Id, string Name, string CompanyCode, string CompanyName, bool IsActive);

public sealed record AdminCompanyOption(string Code, string Name, string? Ruc, bool IsActive);

/// <summary>Opciones de los formularios de usuario (incluye inactivos para mostrarlos deshabilitados).</summary>
public sealed record AdminCatalogResponse(IReadOnlyList<AdminRoleOption> Roles, IReadOnlyList<AdminAreaOption> Areas, IReadOnlyList<AdminCompanyOption> Companies);

/// <summary>Enlace de activación o recuperación enviado al usuario (solo se muestra un fragmento del hash).</summary>
public sealed record PasswordLinkResponse(string Kind, string Fingerprint, DateTime CreatedAtUtc, DateTime ExpiresAtUtc, string Status);

public sealed record PasswordLinkSent(string Kind, string Email);

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
