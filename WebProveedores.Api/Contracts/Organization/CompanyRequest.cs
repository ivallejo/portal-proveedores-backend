using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Api.Contracts.Organization;

public sealed class CompanyRequest
{
    /// <summary>Código de sociedad SAP: 2 a 5 letras o números, único.</summary>
    [Required, MaxLength(5)] public string Code { get; init; } = string.Empty;
    [Required, MaxLength(200)] public string Name { get; init; } = string.Empty;
    [Required, MaxLength(11)] public string Ruc { get; init; } = string.Empty;
    /// <summary>Recibe los comprobantes electrónicos y copia de las notificaciones a proveedores.</summary>
    [Required, MaxLength(320)] public string BillingEmail { get; init; } = string.Empty;
}
