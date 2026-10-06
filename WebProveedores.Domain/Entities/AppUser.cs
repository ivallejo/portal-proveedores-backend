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
    /// <summary>Nombres y apellidos del personal interno; <see cref="CompanyName"/> guarda el nombre completo.</summary>
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string? Ruc { get; private set; }
    /// <summary>DNI del personal interno (8 dígitos); es también su usuario de acceso.</summary>
    public string? Dni { get; private set; }
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
        bool temporaryPassword = false, bool activated = true, string? dni = null, bool emailVerified = true)
    {
        if (string.IsNullOrWhiteSpace(username)) throw new DomainRuleException("El usuario necesita un nombre de usuario.");
        if (string.IsNullOrWhiteSpace(email)) throw new DomainRuleException("El usuario necesita un correo.");
        var user = new AppUser
        {
            Username = username.Trim(),
            CompanyName = name.Trim(),
            Ruc = string.IsNullOrWhiteSpace(ruc) ? null : ruc.Trim(),
            Dni = string.IsNullOrWhiteSpace(dni) ? null : dni.Trim(),
            PasswordHash = passwordHash,
            CreatedAtUtc = now,
            PasswordSetAtUtc = activated ? now : null,
            MustChangePassword = temporaryPassword,
        };
        // Seed y SAP: se da por verificado. El alta del administrador lo verifica al activar la cuenta.
        user.Emails.Add(new UserEmail
        {
            UserId = user.Id,
            Email = email.Trim().ToLowerInvariant(),
            IsPrimary = true,
            CreatedAtUtc = now,
            VerifiedAtUtc = emailVerified ? now : null,
        });
        return user;
    }

    public string PrimaryEmail =>
        Emails.FirstOrDefault(item => item.IsPrimary && item.IsActive)?.Email ?? Emails.FirstOrDefault(item => item.IsActive)?.Email ?? string.Empty;

    /// <summary>Cuenta de proveedor: se identifica con su RUC.</summary>
    public bool IsProvider => Ruc is not null;

    public bool HasRole(string code) => UserRoles.Any(item => item.Role?.Code == code);

    public bool IsLockedOut(DateTime now) => LockoutUntilUtc is { } until && until > now;

    /// <summary>Ya definió su contraseña (activó la cuenta).</summary>
    public bool IsActivated => PasswordSetAtUtc is not null;

    public UserStatus StatusAt(DateTime now) => !IsActive ? UserStatus.Inactive : IsLockedOut(now) ? UserStatus.Locked : UserStatus.Active;

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

    /// <summary>El administrador pide (o deja de pedir) que defina una nueva contraseña en su próximo ingreso.</summary>
    public void RequirePasswordChange(bool required, DateTime now)
    {
        if (MustChangePassword == required) return;
        MustChangePassword = required;
        UpdatedAtUtc = now;
    }

    /// <summary>Activó la cuenta con el enlace enviado al correo principal: ese correo queda verificado.</summary>
    public void ConfirmPrimaryEmail(DateTime now)
    {
        var primary = Emails.FirstOrDefault(item => item.IsPrimary);
        if (primary is { IsVerified: false }) primary.Verify(now);
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
        if (primary is null) Emails.Add(new UserEmail { UserId = Id, Email = normalized, IsPrimary = true, CreatedAtUtc = now, VerifiedAtUtc = now });
        else if (primary.Email != normalized)
        {
            primary.Email = normalized;
            primary.Verify(now);
        }
        UpdatedAtUtc = now;
    }

    // ——— Mi perfil ———

    /// <summary>Razón social del proveedor.</summary>
    public void RenameBusiness(string businessName, DateTime now)
    {
        if (!IsProvider) throw new DomainRuleException("Solo un proveedor tiene razón social.");
        CompanyName = businessName.Trim();
        UpdatedAtUtc = now;
    }

    /// <summary>Nombres y apellidos del personal interno.</summary>
    public void SetPersonName(string firstName, string lastName, DateTime now)
    {
        if (IsProvider) throw new DomainRuleException("Un proveedor se identifica con su razón social.");
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        CompanyName = $"{FirstName} {LastName}".Trim();
        UpdatedAtUtc = now;
    }

    /// <summary>Correo adicional, pendiente de verificación.</summary>
    public UserEmail AddEmail(string email, EmailType type, DateTime now)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (Emails.Any(item => item.Email == normalized)) throw new DomainRuleException("Este correo ya está registrado en tu perfil.");
        var added = new UserEmail { UserId = Id, Email = normalized, Type = type, CreatedAtUtc = now };
        Emails.Add(added);
        UpdatedAtUtc = now;
        return added;
    }

    /// <summary>El correo principal recibe las notificaciones y los enlaces de recuperación: debe estar verificado.</summary>
    public void MakePrimary(Guid emailId, DateTime now)
    {
        var email = OwnEmail(emailId);
        if (!email.IsVerified) throw new DomainRuleException("Verifica el correo antes de hacerlo principal.");
        foreach (var item in Emails) item.IsPrimary = item.Id == emailId;
        UpdatedAtUtc = now;
    }

    public void RemoveEmail(Guid emailId, DateTime now)
    {
        var email = OwnEmail(emailId);
        if (email.IsPrimary) throw new DomainRuleException("El correo principal no se puede eliminar.");
        Emails.Remove(email);
        UpdatedAtUtc = now;
    }

    public void SetEmailType(Guid emailId, EmailType type, DateTime now)
    {
        var email = OwnEmail(emailId);
        if (email.Type == type) return;
        email.Type = type;
        UpdatedAtUtc = now;
    }

    public UserEmail OwnEmail(Guid emailId) =>
        Emails.FirstOrDefault(item => item.Id == emailId) ?? throw new DomainRuleException("El correo no pertenece a tu perfil.");

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

/// <summary>Estado de la cuenta: inactiva (no ingresa), bloqueada temporalmente por intentos fallidos o activa.</summary>
public enum UserStatus
{
    Active = 1,
    Inactive = 2,
    Locked = 3,
}
