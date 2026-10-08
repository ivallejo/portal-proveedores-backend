namespace WebProveedores.Application.Contracts.Payments.Requests;

/// <summary>Filtros comunes. El proveedor siempre consulta su propio RUC; CxP y el administrador indican el RUC.</summary>
public sealed record PaymentSearchRequest(string? Ruc, string? CompanyCode, DateOnly From, DateOnly To);
