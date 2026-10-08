namespace WebProveedores.Application.Access.Responses;

public sealed record MenuAdminResponse(
    Guid Id, string Code, string Name, string? Route, string Icon, int Order, Guid? ParentId, bool IsActive, bool IsSystem, int RoleCount);
