namespace WebProveedores.Application.Contracts.Auth.Responses;

public sealed record PasswordResetResponse(bool Sent, string MaskedEmail);
