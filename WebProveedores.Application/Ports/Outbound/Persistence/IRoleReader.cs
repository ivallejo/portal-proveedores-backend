using WebProveedores.Domain.Access;

namespace WebProveedores.Application.Ports.Outbound.Persistence;

/// <summary>Roles con seguimiento, para asignarlos a usuarios.</summary>
public interface IRoleReader
{
    Task<Role?> FindRoleAsync(string code, CancellationToken cancellationToken);
    /// <summary>Roles activos.</summary>
    Task<IReadOnlyList<Role>> ListRolesAsync(CancellationToken cancellationToken);
}
