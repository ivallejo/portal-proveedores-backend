namespace WebProveedores.Application.Contracts.Profile.Responses;

public sealed record ProfileResponse(
    string Username,
    bool IsProvider,
    string? Ruc,
    string DisplayName,
    string? BusinessName,
    string? FirstName,
    string? LastName,
    IReadOnlyList<string> Roles,
    string? AreaName,
    string? AreaCompanyName,
    IReadOnlyList<ProfileCompanyResponse> Companies,
    IReadOnlyList<ProfileEmailResponse> Emails,
    bool MustChangePassword,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? PasswordSetAtUtc);
