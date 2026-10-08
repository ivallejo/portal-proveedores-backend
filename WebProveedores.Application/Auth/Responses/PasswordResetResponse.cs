namespace WebProveedores.Application.Auth.Responses;

public sealed record PasswordResetResponse(bool Sent, string MaskedEmail);
