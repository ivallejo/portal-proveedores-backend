namespace WebProveedores.Application.Ports.Outbound.Persistence;

/// <summary>Permisos efectivos de un usuario según sus roles y las opciones de menú asignadas.</summary>
public interface IPermissionReader
{
    /// <summary>Códigos de las opciones activas de los roles activos del usuario.</summary>
    Task<IReadOnlySet<string>> PermissionsOfAsync(Guid userId, CancellationToken cancellationToken);
}
