using WebProveedores.Application.Access.Commands;
using WebProveedores.Application.Access.Responses;

namespace WebProveedores.Application.Access;

/// <summary>Configuración › Roles y permisos y Menús.</summary>
public interface IAccessAdminService
{
    Task<IReadOnlyList<RoleAdminResponse>> ListRolesAsync(CancellationToken cancellationToken);
    Task<RoleAdminResponse> CreateRoleAsync(SaveRoleCommand request, CancellationToken cancellationToken);
    Task<RoleAdminResponse> UpdateRoleAsync(Guid id, SaveRoleCommand request, CancellationToken cancellationToken);
    Task<RoleAdminResponse> SetRoleStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken);
    Task<IReadOnlyList<MenuAdminResponse>> ListMenusAsync(CancellationToken cancellationToken);
    Task<MenuAdminResponse> CreateMenuAsync(SaveMenuCommand request, CancellationToken cancellationToken);
    Task<MenuAdminResponse> UpdateMenuAsync(Guid id, SaveMenuCommand request, CancellationToken cancellationToken);
    Task<MenuAdminResponse> SetMenuStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken);
}
