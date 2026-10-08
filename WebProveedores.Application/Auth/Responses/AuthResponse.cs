namespace WebProveedores.Application.Auth.Responses;

public sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, UserResponse User);
