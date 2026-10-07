using System.ComponentModel.DataAnnotations;
using WebProveedores.Domain.Access;
using WebProveedores.Domain.Identity;
using WebProveedores.Domain.Organization;

namespace WebProveedores.Application.Auth;

public sealed class RegisterRequest
{
    [Required, MaxLength(20)] public string Ruc { get; init; } = string.Empty;
    [Required, MaxLength(200)] public string CompanyName { get; init; } = string.Empty;
    [Required, EmailAddress] public string Email { get; init; } = string.Empty;
    [Required, MinLength(8)] public string Password { get; init; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required, MaxLength(320)] public string Identifier { get; init; } = string.Empty;
    [Required] public string Password { get; init; } = string.Empty;
}
/// <summary>La cuenta está bloqueada temporalmente por demasiados intentos fallidos.</summary>
public sealed class AccountLockedException(TimeSpan retryAfter) : Exception("Demasiados intentos fallidos. Espera unos minutos antes de volver a intentarlo.")
{
    public TimeSpan RetryAfter { get; } = retryAfter;
}

public sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, UserResponse User);
public sealed record UserResponse(Guid Id, string Username, string Email, string CompanyName, string Ruc, string? Area, string Role, IReadOnlyList<string> Roles, bool MustChangePassword = false);

public sealed class ChangePasswordRequest
{
    /// <summary>
    /// Obligatoria en el cambio voluntario. En el cambio forzado de la contraseña temporal no se pide:
    /// la sesión se abrió con ella y solo permite cambiar la contraseña.
    /// </summary>
    public string? CurrentPassword { get; init; }
    [Required, MinLength(8), MaxLength(128)] public string NewPassword { get; init; } = string.Empty;
}
public sealed class ValidateRucRequest
{
    [Required, RegularExpression(@"^\d{11}$")] public string Ruc { get; init; } = string.Empty;
}

public sealed class RequestAccessKeyRequest
{
    [Required, RegularExpression(@"^\d{11}$")] public string Ruc { get; init; } = string.Empty;
    [Range(typeof(bool), "true", "true")] public bool TermsAccepted { get; init; }
}

public sealed record ProviderLookupResponse(string Ruc, string CompanyName, string MaskedEmail);
public sealed record AccessKeyResponse(bool Sent, string MaskedEmail);
public sealed class PasswordResetRequest
{
    [Required, RegularExpression(@"^\d{11}$")] public string Ruc { get; init; } = string.Empty;
}

/// <summary>Enlace de activación o recuperación: identifica la cuenta con el RUC (proveedor) o el usuario (personal interno).</summary>
public sealed class PasswordResetConfirmRequest
{
    [RegularExpression(@"^\d{11}$")] public string? Ruc { get; init; }
    [MaxLength(80)] public string? User { get; init; }
    [Required] public string Token { get; init; } = string.Empty;
    [Required, MinLength(8), MaxLength(128)] public string NewPassword { get; init; } = string.Empty;
}

public sealed record PasswordResetResponse(bool Sent, string MaskedEmail);

public interface ILoginService
{
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<UserResponse?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);
}

public interface IPasswordService
{
    /// <summary>
    /// Cambia la contraseña del usuario en sesión y devuelve una sesión nueva (sin la marca de cambio pendiente).
    /// <paramref name="passwordChangeSession"/>: la sesión se abrió con la contraseña temporal.
    /// </summary>
    Task<AuthResponse> ChangePasswordAsync(Guid userId, bool passwordChangeSession, ChangePasswordRequest request, CancellationToken cancellationToken);
    Task<PasswordResetResponse?> RequestPasswordResetAsync(PasswordResetRequest request, CancellationToken cancellationToken);
    Task<bool> ConfirmPasswordResetAsync(PasswordResetConfirmRequest request, PasswordTokenPurpose purpose, CancellationToken cancellationToken);
}

public interface IProviderRegistrationService
{
    Task<ProviderLookupResponse> ValidateRucAsync(string ruc, CancellationToken cancellationToken);
    Task<AccessKeyResponse> RequestAccessKeyAsync(string ruc, CancellationToken cancellationToken);
    /// <summary>Alta directa con contraseña (solo administrador).</summary>
    Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
}
