namespace WebProveedores.Domain.Entities;

public sealed class UserRole
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;

    public AppUser User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
