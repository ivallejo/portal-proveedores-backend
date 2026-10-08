namespace WebProveedores.Application.Contracts.Profile.Responses;

public sealed record ProfileCompanyResponse(string Code, string Name, string? Ruc, bool IsActive);
