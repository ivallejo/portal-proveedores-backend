namespace WebProveedores.Domain.Access;

public sealed class RoleMenu
{
    public Guid RoleId { get; set; }
    public Guid MenuOptionId { get; set; }

    public Role Role { get; set; } = null!;
    public MenuOption MenuOption { get; set; } = null!;
}
