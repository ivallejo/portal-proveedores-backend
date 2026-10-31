namespace WebProveedores.Application.Profile.Responses;

public sealed record ProfileResponse(
    string Username,
    bool IsProvider,
    string? Ruc,
    /// <summary>RUC, DNI, CE o PASAPORTE (o «Usuario» si un interno antiguo no tiene documento).</summary>
    string DocumentType,
    string Document,
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
