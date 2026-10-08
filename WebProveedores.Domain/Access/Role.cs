namespace WebProveedores.Domain.Access;

public sealed class Role
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<RoleMenu> RoleMenus { get; set; } = [];

    /// <summary>Rol base del portal (proveedor, interno, aprobador, cuentas por pagar, administrador).</summary>
    public bool IsSystem => SecurityCatalog.Roles.ContainsKey(Code);
}
