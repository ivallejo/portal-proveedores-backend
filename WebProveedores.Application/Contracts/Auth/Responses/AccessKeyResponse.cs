namespace WebProveedores.Application.Contracts.Auth.Responses;

public sealed record AccessKeyResponse(bool Sent, string MaskedEmail);
