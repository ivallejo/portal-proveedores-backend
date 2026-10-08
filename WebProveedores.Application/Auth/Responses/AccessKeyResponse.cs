namespace WebProveedores.Application.Auth.Responses;

public sealed record AccessKeyResponse(bool Sent, string MaskedEmail);
