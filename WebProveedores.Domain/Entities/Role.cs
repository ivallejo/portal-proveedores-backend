namespace WebProveedores.Domain.Entities;

public sealed class Role
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<UserRole> UserRoles { get; set; } = [];
}
