using System.ComponentModel.DataAnnotations;
using WebProveedores.Domain.Entities;

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
public sealed record UserResponse(Guid Id, string Username, string Email, string CompanyName, string Ruc, string? Area, string Role, IReadOnlyList<string> Roles);
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

public sealed class PasswordResetConfirmRequest
{
    [Required, RegularExpression(@"^\d{11}$")] public string Ruc { get; init; } = string.Empty;
    [Required] public string Token { get; init; } = string.Empty;
    [Required, MinLength(6)] public string NewPassword { get; init; } = string.Empty;
}

public sealed record PasswordResetResponse(bool Sent, string MaskedEmail);

public interface IAuthService
{
    Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<PasswordResetResponse?> RequestPasswordResetAsync(PasswordResetRequest request, CancellationToken cancellationToken);
    Task<bool> ConfirmPasswordResetAsync(PasswordResetConfirmRequest request, PasswordTokenPurpose purpose, CancellationToken cancellationToken);
    Task<UserResponse?> GetCurrentUserAsync(System.Security.Claims.ClaimsPrincipal principal, CancellationToken cancellationToken);
}

public interface IOnlineRegistrationService
{
    Task<ProviderLookupResponse> ValidateRucAsync(string ruc, CancellationToken cancellationToken);
    Task<AccessKeyResponse> RequestAccessKeyAsync(string ruc, CancellationToken cancellationToken);
}
