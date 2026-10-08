namespace WebProveedores.Application.Organization.Responses;

public sealed record AreaAdminResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid CompanyId,
    string CompanyCode,
    string CompanyName,
    bool IsActive,
    int UserCount);
