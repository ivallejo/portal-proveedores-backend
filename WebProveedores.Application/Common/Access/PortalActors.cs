using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Application.Ports.Outbound.Persistence;

namespace WebProveedores.Application.Common.Access;

/// <summary>Carga al usuario en sesión con sus roles, permisos y sociedades (<see cref="PortalActor"/>).</summary>
internal sealed class PortalActors(IUserQueries users, IPermissionReader permissionReader)
{
    public async Task<PortalActor> LoadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new ForbiddenException("La sesión no es válida.");
        var email = user.Emails.FirstOrDefault(item => item.IsPrimary && item.IsActive)?.Email ?? user.Emails.FirstOrDefault(item => item.IsActive)?.Email ?? string.Empty;
        var roles = user.UserRoles.Where(item => item.Role.IsActive).Select(item => item.Role.Code).ToHashSet();
        var companies = user.UserCompanies.Select(item => item.CompanyId).ToHashSet();
        var permissions = await permissionReader.PermissionsOfAsync(user.Id, cancellationToken);
        return new PortalActor(user.Id, user.CompanyName, email, user.Ruc, user.AreaId, user.Area?.Name, roles, permissions, companies);
    }
}
