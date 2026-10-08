namespace WebProveedores.Application.Contracts.Admin.Responses;

public sealed record AdminRoleOption(string Code, string Name, string? Description, bool IsProvider, bool IsActive);
