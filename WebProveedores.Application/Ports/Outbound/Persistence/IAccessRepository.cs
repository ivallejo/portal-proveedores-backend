using WebProveedores.Application.Ports.Outbound.Persistence.Models;
using WebProveedores.Domain.Access;

namespace WebProveedores.Application.Ports.Outbound.Persistence;

/// <summary>Roles y opciones de menú para su administración (con seguimiento).</summary>
public interface IAccessRepository
{
    /// <summary>Todas las opciones con sus asignaciones a roles.</summary>
    Task<IReadOnlyList<MenuOption>> ListMenusAsync(CancellationToken cancellationToken);
    /// <summary>Todos los roles con sus opciones y cuántos usuarios tiene cada uno.</summary>
    Task<IReadOnlyList<RoleSummary>> ListRolesAsync(CancellationToken cancellationToken);
    void Add(Role role);
    void Add(MenuOption menu);
}
