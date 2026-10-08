namespace WebProveedores.Application.Access.Responses;

public sealed record RoleAdminResponse(
    Guid Id, string Code, string Name, string? Description, bool IsActive, bool IsSystem, int UserCount, IReadOnlyList<Guid> MenuIds);
