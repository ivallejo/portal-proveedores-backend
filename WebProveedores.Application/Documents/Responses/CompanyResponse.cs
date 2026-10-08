namespace WebProveedores.Application.Documents.Responses;

/// <param name="BillingEmail">Correo de facturación de la sociedad (recepción de comprobantes electrónicos).</param>
public sealed record CompanyResponse(string Code, string Name, string? Ruc, string? BillingEmail);
