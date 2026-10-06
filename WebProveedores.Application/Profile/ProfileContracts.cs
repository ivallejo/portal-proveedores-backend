using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Application.Profile;

/// <summary>Datos que la persona puede cambiar: razón social (proveedor) o nombres y apellidos (personal interno).</summary>
public sealed class UpdateProfileRequest
{
    [MaxLength(200)] public string? BusinessName { get; init; }
    [MaxLength(100)] public string? FirstName { get; init; }
    [MaxLength(100)] public string? LastName { get; init; }
}

public sealed class AddEmailRequest
{
    [Required, MaxLength(320)] public string Email { get; init; } = string.Empty;
    /// <summary>work, billing o personal.</summary>
    [Required] public string Type { get; init; } = "work";
}

public sealed class VerifyEmailRequest
{
    [Required] public string Token { get; init; } = string.Empty;
}

public sealed record ProfileEmailResponse(Guid Id, string Email, string Type, bool IsPrimary, bool IsVerified, DateTime CreatedAtUtc);

public sealed record ProfileCompanyResponse(string Code, string Name, string? Ruc, bool IsActive);

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

/// <summary>Mi perfil: datos, correos (con verificación) de quien tiene sesión.</summary>
public interface IProfileService
{
    Task<ProfileResponse> GetAsync(Guid userId, CancellationToken cancellationToken);
    Task<ProfileResponse> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken);
    Task<ProfileResponse> AddEmailAsync(Guid userId, AddEmailRequest request, CancellationToken cancellationToken);
    Task<ProfileResponse> ResendVerificationAsync(Guid userId, Guid emailId, CancellationToken cancellationToken);
    Task<ProfileResponse> MakePrimaryAsync(Guid userId, Guid emailId, CancellationToken cancellationToken);
    Task<ProfileResponse> RemoveEmailAsync(Guid userId, Guid emailId, CancellationToken cancellationToken);
    /// <summary>Enlace del correo de verificación (sin sesión). Devuelve el correo verificado o null si el enlace no sirve.</summary>
    Task<string?> VerifyEmailAsync(string token, CancellationToken cancellationToken);
}
