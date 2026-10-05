using WebProveedores.Domain.Documents;

namespace WebProveedores.Domain.Entities;

/// <summary>
/// Cuenta del portal (proveedor o personal interno). Su estado de seguridad —contraseña, bloqueo, activación—
/// solo cambia a través de sus métodos, que aplican las reglas.
/// </summary>
public sealed class AppUser
{
    // Para EF Core.
    private AppUser() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string Username { get; private set; } = string.Empty;
    /// <summary>Nombre de la persona o razón social del proveedor.</summary>
    public string CompanyName { get; private set; } = string.Empty;
    public string? Ruc { get; private set; }
    public string PasswordHash { get; private set; } = string.Empty;
    public Guid? AreaId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    /// <summary>Cuándo definió su contraseña; vacío mientras la cuenta no se active.</summary>
    public DateTime? PasswordSetAtUtc { get; private set; }

    /// <summary>La contraseña actual es temporal: debe cambiarla antes de usar el portal.</summary>
    public bool MustChangePassword { get; private set; }

    /// <summary>Intentos de acceso fallidos desde el último ingreso correcto.</summary>
    public int FailedLoginCount { get; private set; }

    /// <summary>Mientras sea futura, la cuenta no acepta contraseñas (bloqueo temporal).</summary>
    public DateTime? LockoutUntilUtc { get; private set; }

    public Area? Area { get; private set; }
    public ICollection<UserEmail> Emails { get; private set; } = [];
    public ICollection<UserRole> UserRoles { get; private set; } = [];
    public ICollection<UserCompany> UserCompanies { get; private set; } = [];
    public ICollection<PasswordResetToken> PasswordResetTokens { get; private set; } = [];

    /// <summary>
    /// Cuenta nueva. <paramref name="temporaryPassword"/>: la contraseña la definió otra persona (administrador, seed)
    /// y debe cambiarla al ingresar. <paramref name="activated"/> = false: aún no definió su contraseña (registro online).
    /// </summary>
    public static AppUser Create(string username, string name, string? ruc, string email, string passwordHash, DateTime now,
        bool temporaryPassword = false, bool activated = true)
    {
        if (string.IsNullOrWhiteSpace(username)) throw new DomainRuleException("El usuario necesita un nombre de usuario.");
        if (string.IsNullOrWhiteSpace(email)) throw new DomainRuleException("El usuario necesita un correo.");
        var user = new AppUser
        {
            Username = username.Trim(),
            CompanyName = name.Trim(),
            Ruc = string.IsNullOrWhiteSpace(ruc) ? null : ruc.Trim(),
            PasswordHash = passwordHash,
            CreatedAtUtc = now,
            PasswordSetAtUtc = activated ? now : null,
            MustChangePassword = temporaryPassword,
        };
        user.Emails.Add(new UserEmail { UserId = user.Id, Email = email.Trim().ToLowerInvariant(), IsPrimary = true, CreatedAtUtc = now });
        return user;
    }

    public string PrimaryEmail =>
        Emails.FirstOrDefault(item => item.IsPrimary && item.IsActive)?.Email ?? Emails.FirstOrDefault(item => item.IsActive)?.Email ?? string.Empty;

    public bool HasRole(string code) => UserRoles.Any(item => item.Role?.Code == code);

    public bool IsLockedOut(DateTime now) => LockoutUntilUtc is { } until && until > now;

    // ——— Acceso ———

    /// <summary>Suma un intento fallido; al llegar al máximo bloquea la cuenta unos minutos y reinicia el contador.</summary>
    public void RecordFailedLogin(int maxFailedLogins, int lockoutMinutes, DateTime now)
    {
        FailedLoginCount++;
        if (FailedLoginCount < maxFailedLogins) return;
        LockoutUntilUtc = now.AddMinutes(lockoutMinutes);
        FailedLoginCount = 0;
    }

    /// <summary>Ingreso correcto o desbloqueo manual: borra los intentos y el bloqueo. Devuelve si había algo que limpiar.</summary>
    public bool ClearFailedLogins()
    {
        if (FailedLoginCount == 0 && LockoutUntilUtc is null) return false;
        FailedLoginCount = 0;
        LockoutUntilUtc = null;
        return true;
    }

    public void Unlock(DateTime now)
    {
        if (ClearFailedLogins()) UpdatedAtUtc = now;
    }

    /// <summary>Contraseña definida por la propia persona (cambio, activación o recuperación): deja de ser temporal.</summary>
    public void SetPassword(string passwordHash, DateTime now)
    {
        PasswordHash = passwordHash;
        MustChangePassword = false;
        PasswordSetAtUtc = now;
        UpdatedAtUtc = now;
    }

    public void SetActive(bool isActive, DateTime now)
    {
        IsActive = isActive;
        UpdatedAtUtc = now;
    }

    // ——— Datos y permisos ———

    public void UpdateProfile(string name, string email, DateTime now)
    {
        CompanyName = name.Trim();
        var normalized = email.Trim().ToLowerInvariant();
        var primary = Emails.FirstOrDefault(item => item.IsPrimary && item.IsActive) ?? Emails.FirstOrDefault(item => item.IsActive);
        if (primary is null) Emails.Add(new UserEmail { UserId = Id, Email = normalized, IsPrimary = true, CreatedAtUtc = now });
        else primary.Email = normalized;
        UpdatedAtUtc = now;
    }

    /// <summary>Registro online repetido de una cuenta aún no activada: datos de SAP actualizados y cuenta activa.</summary>
    public void RefreshPendingProvider(string name, string email, DateTime now)
    {
        if (PasswordSetAtUtc is not null) throw new DomainRuleException("La cuenta ya está activa.");
        UpdateProfile(name, email, now);
        IsActive = true;
    }

    public void AssignArea(Area? area)
    {
        Area = area;
        AreaId = area?.Id;
    }

    /// <summary>Deja asignados exactamente estos roles.</summary>
    public void SetRoles(IEnumerable<Role> roles)
    {
        var wanted = roles.DistinctBy(role => role.Id).ToList();
        foreach (var stale in UserRoles.Where(item => wanted.All(role => role.Id != item.RoleId)).ToList())
            UserRoles.Remove(stale);
        foreach (var role in wanted.Where(role => UserRoles.All(item => item.RoleId != role.Id)))
            UserRoles.Add(new UserRole { UserId = Id, RoleId = role.Id, Role = role });
    }

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
