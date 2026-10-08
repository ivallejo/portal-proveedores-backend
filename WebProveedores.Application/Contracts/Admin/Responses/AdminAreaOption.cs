namespace WebProveedores.Application.Contracts.Admin.Responses;

public sealed record AdminAreaOption(Guid Id, string Name, string CompanyCode, string CompanyName, bool IsActive);
