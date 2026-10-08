using WebProveedores.Application.Ports.Outbound.Persistence;

namespace WebProveedores.Application.Access;

internal sealed class PermissionService(IPermissionReader permissionReader) : IPermissionService
{
    public Task<IReadOnlySet<string>> PermissionsOfAsync(Guid userId, CancellationToken cancellationToken) =>
        permissionReader.PermissionsOfAsync(userId, cancellationToken);
}
