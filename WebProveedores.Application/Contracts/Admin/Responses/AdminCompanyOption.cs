namespace WebProveedores.Application.Contracts.Admin.Responses;

public sealed record AdminCompanyOption(string Code, string Name, string? Ruc, bool IsActive);
