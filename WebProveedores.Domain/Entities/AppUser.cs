using WebProveedores.Domain.Documents;

namespace WebProveedores.Domain.Entities;

public sealed class AppUser
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Username { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? Ruc { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public Guid? AreaId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? PasswordSetAtUtc { get; set; }

    /// <summary>La contraseña actual es temporal: debe cambiarla antes de usar el portal.</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>Intentos de acceso fallidos desde el último ingreso correcto.</summary>
    public int FailedLoginCount { get; set; }

    /// <summary>Mientras sea futura, la cuenta no acepta contraseñas (bloqueo temporal).</summary>
    public DateTime? LockoutUntilUtc { get; set; }

    public Area? Area { get; set; }
    public ICollection<UserEmail> Emails { get; set; } = [];
    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<UserCompany> UserCompanies { get; set; } = [];
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = [];

    /// <summary>Deja asignadas exactamente estas sociedades (quita las demás y agrega las que falten).</summary>
    public void SetCompanies(IEnumerable<Company> companies)
    {
        var wanted = companies.DistinctBy(company => company.Id).ToList();
        foreach (var stale in UserCompanies.Where(item => wanted.All(company => company.Id != item.CompanyId)).ToList())
            UserCompanies.Remove(stale);
        foreach (var company in wanted.Where(company => UserCompanies.All(item => item.CompanyId != company.Id)))
            UserCompanies.Add(new UserCompany { UserId = Id, CompanyId = company.Id, Company = company });
    }
}
