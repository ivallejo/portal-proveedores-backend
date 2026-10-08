using WebProveedores.Application.Ports.Inbound.Access;
using WebProveedores.Application.Ports.Outbound.Persistence;

namespace WebProveedores.Application.UseCases.Access;

internal sealed class PermissionService(IPermissionReader permissionReader) : IPermissionService
{
    public Task<IReadOnlySet<string>> PermissionsOfAsync(Guid userId, CancellationToken cancellationToken) =>
        permissionReader.PermissionsOfAsync(userId, cancellationToken);
}
