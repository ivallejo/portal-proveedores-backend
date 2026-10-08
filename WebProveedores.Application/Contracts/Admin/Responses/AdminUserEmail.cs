namespace WebProveedores.Application.Contracts.Admin.Responses;

public sealed record AdminUserEmail(Guid Id, string Email, string Type, bool IsPrimary, bool IsVerified, DateTime CreatedAtUtc);
