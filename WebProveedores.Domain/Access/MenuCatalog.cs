namespace WebProveedores.Domain.Access;

/// <summary>Opciones del sistema (pantallas del portal), su orden inicial y qué roles base las reciben al crearlas.</summary>
public static class MenuCatalog
{
    public const string Home = "HOME";
    public const string PaymentOrders = "PAYMENT_ORDERS";
    public const string InvoiceStatus = "INVOICE_STATUS";
    public const string Documents = "DOCUMENTS";
    public const string RegisterDocuments = "REGISTER_DOCUMENTS";
    public const string Accounting = "ACCOUNTING";
    public const string Settings = "SETTINGS";
    public const string SettingsCompanies = "SETTINGS_COMPANIES";
    public const string SettingsAreas = "SETTINGS_AREAS";
    public const string SettingsUsers = "SETTINGS_USERS";
    public const string SettingsRoles = "SETTINGS_ROLES";
    public const string SettingsMenus = "SETTINGS_MENUS";

    private const string P = SecurityCatalog.ProviderRole, I = SecurityCatalog.InternalUserRole, A = SecurityCatalog.AreaApproverRole,
        C = SecurityCatalog.AccountsPayableRole, D = SecurityCatalog.AdministratorRole;

    public static readonly IReadOnlyList<MenuCatalogEntry> System =
    [
        new(Home, "Inicio", "/inicio", "home", 1, null, [P, I, A, C, D]),
        new(PaymentOrders, "Orden de pago", "/orden-pago", "cash", 2, null, [P, C, D]),
        new(InvoiceStatus, "Estado de factura", "/estado-factura", "file-lines", 3, null, [P, C, D]),
        new(Documents, "Documentos", "/documentos", "file-text", 4, null, [A, D]),
        new(RegisterDocuments, "Registrar documentos", "/registrar-documento", "file-upload", 5, null, [P, I, D]),
        new(Accounting, "Contabilización", "/contabilizacion", "chart-bar", 6, null, [C, D]),
        new(Settings, "Configuración", null, "settings", 7, null, [D]),
        new(SettingsCompanies, "Sociedades", "/configuracion/sociedades", "building", 1, Settings, [D]),
        new(SettingsAreas, "Áreas", "/configuracion/areas", "layers", 2, Settings, [D]),
        new(SettingsUsers, "Usuarios", "/configuracion/usuarios", "users", 3, Settings, [D]),
        new(SettingsRoles, "Roles y permisos", "/configuracion/roles", "shield-check", 4, Settings, [D]),
        new(SettingsMenus, "Menús", "/configuracion/menus", "menu", 5, Settings, [D]),
    ];

    /// <summary>Sin estas opciones nadie podría volver a administrar roles y menús: no se desactivan ni se quitan al administrador.</summary>
    public static readonly IReadOnlySet<string> Protected = new HashSet<string> { Settings, SettingsRoles, SettingsMenus };

    public static bool IsSystem(string code) => System.Any(entry => entry.Code == code);
}
