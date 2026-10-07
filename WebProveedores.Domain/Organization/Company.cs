namespace WebProveedores.Domain.Organization;

/// <summary>Sociedad del grupo que recibe el documento.</summary>
public sealed class Company
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Ruc { get; set; }
    /// <summary>Correo donde la sociedad recibe los comprobantes electrónicos de sus proveedores.</summary>
    public string? BillingEmail { get; set; }
    public bool IsActive { get; set; } = true;
}
