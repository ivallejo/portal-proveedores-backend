using System.ComponentModel.DataAnnotations;
using WebProveedores.Domain.Entities;

namespace WebProveedores.Application.Auth;

public sealed record RegisterRequest(
    [property: Required, MaxLength(20)] string Ruc,
    [property: Required, MaxLength(200)] string CompanyName,
    [property: Required, EmailAddress] string Email,
    [property: Required, MinLength(8)] string Password);
public sealed record LoginRequest([property: Required, EmailAddress] string Email, [property: Required] string Password);
public sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, UserResponse User);
public sealed record UserResponse(Guid Id, string Email, string CompanyName, string Ruc, string Role);

public interface IAuthService
{
    Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    UserResponse? GetCurrentUser(System.Security.Claims.ClaimsPrincipal principal);
}
