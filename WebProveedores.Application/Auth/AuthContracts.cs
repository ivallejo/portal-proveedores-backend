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
public sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, UserResponse User);
public sealed record UserResponse(Guid Id, string Email, string CompanyName, string Ruc, string Role);

public interface IAuthService
{
    Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    UserResponse? GetCurrentUser(System.Security.Claims.ClaimsPrincipal principal);
}
