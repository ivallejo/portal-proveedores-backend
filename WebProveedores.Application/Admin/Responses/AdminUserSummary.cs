namespace WebProveedores.Application.Admin.Responses;

public sealed record AdminUserSummary(
    Guid Id,
    string DisplayName,
    string PrimaryEmail,
    bool IsProvider,
    string Document,
    string DocumentType,
    string? Role,
    string? RoleName,
    string? AreaName,
    IReadOnlyList<string> CompanyCodes,
    string Status,
    bool IsActivated);
