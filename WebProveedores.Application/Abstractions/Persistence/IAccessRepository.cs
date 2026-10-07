using WebProveedores.Domain.Access;

namespace WebProveedores.Application.Abstractions.Persistence;

/// <summary>Roles, opciones de menú y los permisos que resultan de ellos.</summary>
public interface IAccessRepository
{
    /// <summary>Códigos de las opciones activas de los roles activos del usuario.</summary>
    Task<IReadOnlySet<string>> PermissionsOfAsync(Guid userId, CancellationToken cancellationToken);
    /// <summary>Todas las opciones con sus asignaciones a roles (con seguimiento).</summary>
    Task<IReadOnlyList<MenuOption>> ListMenusAsync(CancellationToken cancellationToken);
    /// <summary>Todos los roles con sus opciones (con seguimiento) y cuántos usuarios tiene cada uno.</summary>
    Task<IReadOnlyList<RoleSummary>> ListRolesAsync(CancellationToken cancellationToken);
    void Add(Role role);
    void Add(MenuOption menu);
}

public sealed record RoleSummary(Role Role, int UserCount);
