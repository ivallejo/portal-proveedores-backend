using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Application.Admin;

public sealed class CreateUserRequest
{
    [Required, MaxLength(80)] public string Username { get; init; } = string.Empty;
    [Required, EmailAddress, MaxLength(320)] public string Email { get; init; } = string.Empty;
    [Required, MaxLength(200)] public string Name { get; init; } = string.Empty;
    /// <summary>Obligatorio para proveedores (11 dígitos); no se puede cambiar después.</summary>
    [MaxLength(11)] public string? Ruc { get; init; }
    /// <summary>Contraseña temporal: la persona debe cambiarla al ingresar.</summary>
    [Required] public string Password { get; init; } = string.Empty;
    /// <summary>Códigos de rol (PROVIDER, INTERNAL_USER, AREA_APPROVER, ACCOUNTS_PAYABLE, ADMINISTRATOR).</summary>
    [Required, MinLength(1)] public IReadOnlyList<string> Roles { get; init; } = [];
    public Guid? AreaId { get; init; }
    public IReadOnlyList<string> CompanyCodes { get; init; } = [];
}

/// <summary>Datos editables; el usuario y el RUC identifican la cuenta y no cambian.</summary>
public sealed class UpdateUserRequest
{
    [Required, EmailAddress, MaxLength(320)] public string Email { get; init; } = string.Empty;
    [Required, MaxLength(200)] public string Name { get; init; } = string.Empty;
    [Required, MinLength(1)] public IReadOnlyList<string> Roles { get; init; } = [];
    public Guid? AreaId { get; init; }
    public IReadOnlyList<string> CompanyCodes { get; init; } = [];
}

public sealed record UpdateUserStatusRequest(bool IsActive);

public sealed record AdminUserResponse(
    Guid Id,
    string Username,
    string Email,
    string Name,
    string? Ruc,
    Guid? AreaId,
    string? AreaName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> CompanyCodes,
    bool IsActive,
    bool IsLocked,
    bool MustChangePassword,
    DateTime CreatedAtUtc);

public sealed record AdminUserPage(IReadOnlyList<AdminUserResponse> Items, int Total, int Page, int PageSize);

public sealed record AdminOption(string Code, string Name);

public sealed record AdminAreaOption(Guid Id, string Name);

/// <summary>Opciones de los formularios de usuario.</summary>
public sealed record AdminCatalogResponse(IReadOnlyList<AdminOption> Roles, IReadOnlyList<AdminAreaOption> Areas, IReadOnlyList<AdminOption> Companies);

public interface IAdminUserService
{
    Task<AdminUserPage> SearchAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<AdminCatalogResponse> CatalogAsync(CancellationToken cancellationToken);
    Task<AdminUserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<AdminUserResponse?> UpdateAsync(Guid actorId, Guid id, UpdateUserRequest request, CancellationToken cancellationToken);
    Task<AdminUserResponse?> SetStatusAsync(Guid actorId, Guid id, UpdateUserStatusRequest request, CancellationToken cancellationToken);
    Task<AdminUserResponse?> UnlockAsync(Guid id, CancellationToken cancellationToken);
}
