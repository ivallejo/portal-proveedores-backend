using Microsoft.AspNetCore.Authorization;
using WebProveedores.Domain.Access;

namespace WebProveedores.Api.Security;

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
