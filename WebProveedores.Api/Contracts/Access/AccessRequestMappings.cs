using WebProveedores.Application.Access.Commands;

namespace WebProveedores.Api.Contracts.Access;

/// <summary>Traduce los requests HTTP de Access a los commands de sus casos de uso.</summary>
internal static class AccessRequestMappings
{
    public static SaveRoleCommand ToCommand(this RoleRequest request) => new() { Name = request.Name, Description = request.Description, MenuIds = request.MenuIds };

    public static SaveMenuCommand ToCommand(this MenuRequest request) => new() { Name = request.Name, Route = request.Route, Icon = request.Icon, Order = request.Order, ParentId = request.ParentId };
}
