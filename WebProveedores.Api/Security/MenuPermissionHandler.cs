using Microsoft.AspNetCore.Authorization;
using WebProveedores.Application.Access;

namespace WebProveedores.Api.Security;

/// <summary>Consulta los permisos del usuario en la base (una vez por petición): los cambios de rol aplican de inmediato.</summary>
public sealed class MenuPermissionHandler(ICurrentUser currentUser, IPermissionService permissionService) : AuthorizationHandler<MenuPermissionRequirement>
{
    private IReadOnlySet<string>? permissions;

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, MenuPermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;
        permissions ??= await permissionService.PermissionsOfAsync(currentUser.Id, CancellationToken.None);
        if (requirement.Codes.Any(permissions.Contains)) context.Succeed(requirement);
    }
}
