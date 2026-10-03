namespace WebProveedores.Domain.Entities;

public sealed class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? Ruc { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public Guid? AreaId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? PasswordSetAtUtc { get; set; }

    public Area? Area { get; set; }
    public ICollection<UserEmail> Emails { get; set; } = [];
    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = [];
}
