namespace WebProveedores.Application.Ports.Outbound.Sap.Models;

public sealed record SapDocumentValidation(
    string CompanyCode,
    string ProviderRuc,
    DateOnly IssuedAt,
    string Number,
    decimal Amount,
    bool CheckSunat);
