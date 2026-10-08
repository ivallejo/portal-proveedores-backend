namespace WebProveedores.Application.Admin.Responses;

public sealed record AdminUserDetail(
    Guid Id,
    string Username,
    bool IsProvider,
    string Document,
    string DocumentType,
    string DisplayName,
    string? BusinessName,
    string? FirstName,
    string? LastName,
    string? Role,
    Guid? AreaId,
    IReadOnlyList<string> CompanyCodes,
    IReadOnlyList<AdminUserEmail> Emails,
    string Status,
    bool IsActivated,
    bool MustChangePassword,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
