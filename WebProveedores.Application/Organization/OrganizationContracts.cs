using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Application.Organization;

public sealed class CompanyRequest
{
    /// <summary>Código de sociedad SAP: 2 a 5 letras o números, único.</summary>
    [Required, MaxLength(5)] public string Code { get; init; } = string.Empty;
    [Required, MaxLength(200)] public string Name { get; init; } = string.Empty;
    [Required, MaxLength(11)] public string Ruc { get; init; } = string.Empty;
    /// <summary>Recibe los comprobantes electrónicos y copia de las notificaciones a proveedores.</summary>
    [Required, MaxLength(320)] public string BillingEmail { get; init; } = string.Empty;
}

public sealed class AreaRequest
{
    [Required] public Guid CompanyId { get; init; }
    [Required, MaxLength(120)] public string Name { get; init; } = string.Empty;
    [MaxLength(300)] public string? Description { get; init; }
}

public sealed record StatusRequest(bool IsActive);

public sealed record CompanyAdminResponse(Guid Id, string Code, string Name, string? Ruc, string? BillingEmail, bool IsActive, int AreaCount, int UserCount);

public sealed record AreaAdminResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid CompanyId,
    string CompanyCode,
    string CompanyName,
    bool IsActive,
    int UserCount);
