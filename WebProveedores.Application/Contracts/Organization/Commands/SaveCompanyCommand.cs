namespace WebProveedores.Application.Contracts.Organization.Commands;

public sealed record SaveCompanyCommand
{
    /// <summary>Código de sociedad SAP: 2 a 5 letras o números, único.</summary>
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Ruc { get; init; } = string.Empty;
    /// <summary>Recibe los comprobantes electrónicos y copia de las notificaciones a proveedores.</summary>
    public string BillingEmail { get; init; } = string.Empty;
}
