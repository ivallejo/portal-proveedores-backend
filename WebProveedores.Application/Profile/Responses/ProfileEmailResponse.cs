namespace WebProveedores.Application.Profile.Responses;

public sealed record ProfileEmailResponse(Guid Id, string Email, string Type, bool IsPrimary, bool IsVerified, DateTime CreatedAtUtc);
