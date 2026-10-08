namespace WebProveedores.Application.Organization.Responses;

public sealed record CompanyAdminResponse(Guid Id, string Code, string Name, string? Ruc, string? BillingEmail, bool IsActive, int AreaCount, int UserCount);
