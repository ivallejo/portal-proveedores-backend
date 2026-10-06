using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Application.Access;

/// <summary>Opción del menú lateral de quien tiene sesión (dos niveles).</summary>
public sealed record NavigationItem(string Code, string Name, string? Route, string Icon, IReadOnlyList<NavigationItem> Children);

public sealed class RoleRequest
{
    [Required, MaxLength(120)] public string Name { get; init; } = string.Empty;
    [MaxLength(300)] public string? Description { get; init; }
    public IReadOnlyList<Guid> MenuIds { get; init; } = [];
}

public sealed record RoleAdminResponse(
    Guid Id, string Code, string Name, string? Description, bool IsActive, bool IsSystem, int UserCount, IReadOnlyList<Guid> MenuIds);

public sealed class MenuRequest
{
    [Required, MaxLength(80)] public string Name { get; init; } = string.Empty;
    [MaxLength(200)] public string? Route { get; init; }
    [Required, MaxLength(40)] public string Icon { get; init; } = string.Empty;
    [Range(1, 99)] public int Order { get; init; } = 1;
    public Guid? ParentId { get; init; }
}

public sealed record MenuAdminResponse(
    Guid Id, string Code, string Name, string? Route, string Icon, int Order, Guid? ParentId, bool IsActive, bool IsSystem, int RoleCount);

public interface INavigationService
{
    Task<IReadOnlyList<NavigationItem>> MenuForAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>Configuración › Roles y permisos y Menús.</summary>
public interface IAccessAdminService
{
    Task<IReadOnlyList<RoleAdminResponse>> ListRolesAsync(CancellationToken cancellationToken);
    Task<RoleAdminResponse> CreateRoleAsync(RoleRequest request, CancellationToken cancellationToken);
    Task<RoleAdminResponse> UpdateRoleAsync(Guid id, RoleRequest request, CancellationToken cancellationToken);
    Task<RoleAdminResponse> SetRoleStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken);
    Task<IReadOnlyList<MenuAdminResponse>> ListMenusAsync(CancellationToken cancellationToken);
    Task<MenuAdminResponse> CreateMenuAsync(MenuRequest request, CancellationToken cancellationToken);
    Task<MenuAdminResponse> UpdateMenuAsync(Guid id, MenuRequest request, CancellationToken cancellationToken);
    Task<MenuAdminResponse> SetMenuStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken);
}
