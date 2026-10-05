using WebProveedores.Domain.Documents;

namespace WebProveedores.Domain.Entities;

/// <summary>Sociedad con la que trabaja un usuario: solo registra y ve documentos de sus sociedades.</summary>
public sealed class UserCompany
{
    public Guid UserId { get; set; }
    public Guid CompanyId { get; set; }
    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;

    public AppUser User { get; set; } = null!;
    public Company Company { get; set; } = null!;
}
