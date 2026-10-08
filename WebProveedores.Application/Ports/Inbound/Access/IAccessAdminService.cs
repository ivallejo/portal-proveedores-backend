using WebProveedores.Application.Contracts.Access.Requests;
using WebProveedores.Application.Contracts.Access.Responses;

namespace WebProveedores.Application.Ports.Inbound.Access;

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
