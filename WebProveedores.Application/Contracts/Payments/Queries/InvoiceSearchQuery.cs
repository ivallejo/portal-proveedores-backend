namespace WebProveedores.Application.Contracts.Payments.Queries;

public sealed record InvoiceSearchQuery(string? Ruc, string? CompanyCode, string? Number, DateOnly From, DateOnly To);
