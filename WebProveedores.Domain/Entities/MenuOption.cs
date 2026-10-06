namespace WebProveedores.Domain.Entities;

/// <summary>
/// Opción del menú lateral (dos niveles). Su <see cref="Code"/> es el permiso: un rol ve la opción y usa sus
/// funciones si la tiene asignada. Las opciones del sistema enlazan a pantallas del portal: su código y su ruta no cambian.
/// </summary>
public sealed class MenuOption
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Ruta del portal; vacía en un menú principal que solo agrupa submenús.</summary>
    public string? Route { get; set; }
    public string Icon { get; set; } = "circle";
    public int Order { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public MenuOption? Parent { get; set; }
    public ICollection<MenuOption> Children { get; set; } = [];
    public ICollection<RoleMenu> RoleMenus { get; set; } = [];

    public bool IsSystem => MenuCatalog.IsSystem(Code);
}

public sealed class RoleMenu
{
    public Guid RoleId { get; set; }
    public Guid MenuOptionId { get; set; }

    public Role Role { get; set; } = null!;
    public MenuOption MenuOption { get; set; } = null!;
}

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

    public sealed record Entry(string Code, string Name, string? Route, string Icon, int Order, string? Parent, string[] Roles);

    private const string P = SecurityCatalog.ProviderRole, I = SecurityCatalog.InternalUserRole, A = SecurityCatalog.AreaApproverRole,
        C = SecurityCatalog.AccountsPayableRole, D = SecurityCatalog.AdministratorRole;

    public static readonly IReadOnlyList<Entry> System =
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
