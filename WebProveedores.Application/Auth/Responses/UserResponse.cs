namespace WebProveedores.Application.Auth.Responses;

public sealed record UserResponse(Guid Id, string Username, string Email, string CompanyName, string Ruc, string? Area, string Role, IReadOnlyList<string> Roles, bool MustChangePassword = false);
