namespace WebProveedores.Application.Contracts.Auth.Responses;

public sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, UserResponse User);
