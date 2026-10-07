using Microsoft.AspNetCore.Authorization;
using WebProveedores.Application.Ports.Outbound.Persistence;
using WebProveedores.Domain.Access;

namespace WebProveedores.Api.Infrastructure;

/// <summary>
/// Políticas de autorización por opción de menú: un endpoint pide el permiso de la pantalla que lo usa
/// (Configuración › Roles y permisos define qué opciones tiene cada rol).
/// </summary>
public static class Policies
{
    public const string CompaniesManage = "Menu." + MenuCatalog.SettingsCompanies;
    public const string AreasManage = "Menu." + MenuCatalog.SettingsAreas;
    public const string UsersManage = "Menu." + MenuCatalog.SettingsUsers;
    public const string RolesManage = "Menu." + MenuCatalog.SettingsRoles;
    public const string MenusManage = "Menu." + MenuCatalog.SettingsMenus;
    public const string DocumentsRegister = "Menu." + MenuCatalog.RegisterDocuments;
    public const string DocumentsApprove = "Menu." + MenuCatalog.Documents;
    public const string DocumentsAccount = "Menu." + MenuCatalog.Accounting;
    public const string PaymentOrdersView = "Menu." + MenuCatalog.PaymentOrders;
    public const string InvoicesView = "Menu." + MenuCatalog.InvoiceStatus;
    /// <summary>La lista de opciones la usan Roles y permisos (árbol de permisos) y Menús.</summary>
    public const string MenusView = "Menu.ROLES_OR_MENUS";

    public static void AddMenuPolicies(this AuthorizationOptions options)
    {
        foreach (var entry in MenuCatalog.System)
            options.AddPolicy("Menu." + entry.Code, policy => policy.RequireAuthenticatedUser().AddRequirements(new MenuPermissionRequirement([entry.Code])));
        options.AddPolicy(MenusView, policy => policy.RequireAuthenticatedUser()
            .AddRequirements(new MenuPermissionRequirement([MenuCatalog.SettingsRoles, MenuCatalog.SettingsMenus])));
    }
}

/// <summary>Basta con tener una de las opciones.</summary>
public sealed record MenuPermissionRequirement(IReadOnlyList<string> Codes) : IAuthorizationRequirement;

/// <summary>Consulta los permisos del usuario en la base (una vez por petición): los cambios de rol aplican de inmediato.</summary>
public sealed class MenuPermissionHandler(ICurrentUser currentUser, IPermissionReader permissionReader) : AuthorizationHandler<MenuPermissionRequirement>
{
    private IReadOnlySet<string>? permissions;

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, MenuPermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;
        permissions ??= await permissionReader.PermissionsOfAsync(currentUser.Id, CancellationToken.None);
        if (requirement.Codes.Any(permissions.Contains)) context.Succeed(requirement);
    }
}
