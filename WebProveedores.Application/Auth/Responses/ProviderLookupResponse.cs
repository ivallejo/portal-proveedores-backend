namespace WebProveedores.Application.Auth.Responses;

public sealed record ProviderLookupResponse(string Ruc, string CompanyName, string MaskedEmail);
