namespace WebProveedores.Domain.Entities;

public sealed class UserEmail
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public EmailType Type { get; set; } = EmailType.Work;
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Cuándo la persona confirmó que el correo es suyo; solo un correo verificado sirve para ingresar o ser principal.</summary>
    public DateTime? VerifiedAtUtc { get; set; }
    /// <summary>Hash del enlace de verificación pendiente (el token no se guarda).</summary>
    public string? VerificationTokenHash { get; set; }
    public DateTime? VerificationExpiresAtUtc { get; set; }

    public bool IsVerified => VerifiedAtUtc is not null;

    public AppUser User { get; set; } = null!;

    /// <summary>Nuevo enlace de verificación (reemplaza al anterior).</summary>
    public void StartVerification(string tokenHash, DateTime expiresAtUtc)
    {
        if (IsVerified) throw new DomainRuleException("El correo ya está verificado.");
        VerificationTokenHash = tokenHash;
        VerificationExpiresAtUtc = expiresAtUtc;
    }

    public void Verify(DateTime now)
    {
        VerifiedAtUtc = now;
        VerificationTokenHash = null;
        VerificationExpiresAtUtc = null;
    }
}

/// <summary>Para qué usa la persona el correo (solo informativo).</summary>
public enum EmailType
{
    Work = 1,
    Billing = 2,
    Personal = 3,
}
