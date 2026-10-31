namespace WebProveedores.Application.Organization.Responses;

public sealed record AreaAdminResponse(
    Guid Id,
    string Name,
    string? Description,
    /// <summary>Sociedades en las que existe el área, por código.</summary>
    IReadOnlyList<AreaCompanyResponse> Companies,
    bool IsActive,
    int UserCount);
