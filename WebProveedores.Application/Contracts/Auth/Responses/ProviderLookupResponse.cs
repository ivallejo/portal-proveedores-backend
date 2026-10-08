namespace WebProveedores.Application.Contracts.Auth.Responses;

public sealed record ProviderLookupResponse(string Ruc, string CompanyName, string MaskedEmail);
