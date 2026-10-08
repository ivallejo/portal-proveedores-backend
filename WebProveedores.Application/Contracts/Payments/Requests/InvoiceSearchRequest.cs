namespace WebProveedores.Application.Contracts.Payments.Requests;

public sealed record InvoiceSearchRequest(string? Ruc, string? CompanyCode, string? Number, DateOnly From, DateOnly To);
