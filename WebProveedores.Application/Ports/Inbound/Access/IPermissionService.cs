namespace WebProveedores.Application.Ports.Inbound.Access;

/// <summary>Permisos efectivos del usuario, para autorizar cada petición según las opciones de menú de sus roles.</summary>
public interface IPermissionService
{
    /// <summary>Códigos de las opciones activas de los roles activos del usuario.</summary>
    Task<IReadOnlySet<string>> PermissionsOfAsync(Guid userId, CancellationToken cancellationToken);
}
