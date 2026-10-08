namespace WebProveedores.Domain.Access;

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
