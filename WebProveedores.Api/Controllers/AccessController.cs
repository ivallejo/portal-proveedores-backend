using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebProveedores.Api.Contracts.Access;
using WebProveedores.Api.Security;
using WebProveedores.Application.Contracts.Access.Responses;
using WebProveedores.Application.Contracts.Organization.Requests;
using WebProveedores.Application.Ports.Inbound.Access;

namespace WebProveedores.Api.Controllers;

/// <summary>Configuración › Roles y permisos y Menús.</summary>
[ApiController]
[Route("api/admin")]
[Authorize]
public sealed class AccessController(IAccessAdminService access) : ControllerBase
{
    [HttpGet("roles")]
    [Authorize(Policy = Policies.RolesManage)]
    public async Task<ActionResult<IReadOnlyList<RoleAdminResponse>>> Roles(CancellationToken cancellationToken) =>
        Ok(await access.ListRolesAsync(cancellationToken));

    [HttpPost("roles")]
    [Authorize(Policy = Policies.RolesManage)]
    public async Task<ActionResult<RoleAdminResponse>> CreateRole(RoleRequest request, CancellationToken cancellationToken) =>
        Ok(await access.CreateRoleAsync(request.ToCommand(), cancellationToken));

    [HttpPut("roles/{id:guid}")]
    [Authorize(Policy = Policies.RolesManage)]
    public async Task<ActionResult<RoleAdminResponse>> UpdateRole(Guid id, RoleRequest request, CancellationToken cancellationToken) =>
        Ok(await access.UpdateRoleAsync(id, request.ToCommand(), cancellationToken));

    [HttpPatch("roles/{id:guid}/status")]
    [Authorize(Policy = Policies.RolesManage)]
    public async Task<ActionResult<RoleAdminResponse>> SetRoleStatus(Guid id, StatusRequest request, CancellationToken cancellationToken) =>
        Ok(await access.SetRoleStatusAsync(id, request.IsActive, cancellationToken));

    /// <summary>También lo usa Roles y permisos para armar el árbol de permisos.</summary>
    [HttpGet("menus")]
    [Authorize(Policy = Policies.MenusView)]
    public async Task<ActionResult<IReadOnlyList<MenuAdminResponse>>> Menus(CancellationToken cancellationToken) =>
        Ok(await access.ListMenusAsync(cancellationToken));

    [HttpPost("menus")]
    [Authorize(Policy = Policies.MenusManage)]
    public async Task<ActionResult<MenuAdminResponse>> CreateMenu(MenuRequest request, CancellationToken cancellationToken) =>
        Ok(await access.CreateMenuAsync(request.ToCommand(), cancellationToken));

    [HttpPut("menus/{id:guid}")]
    [Authorize(Policy = Policies.MenusManage)]
    public async Task<ActionResult<MenuAdminResponse>> UpdateMenu(Guid id, MenuRequest request, CancellationToken cancellationToken) =>
        Ok(await access.UpdateMenuAsync(id, request.ToCommand(), cancellationToken));

    [HttpPatch("menus/{id:guid}/status")]
    [Authorize(Policy = Policies.MenusManage)]
    public async Task<ActionResult<MenuAdminResponse>> SetMenuStatus(Guid id, StatusRequest request, CancellationToken cancellationToken) =>
        Ok(await access.SetMenuStatusAsync(id, request.IsActive, cancellationToken));
}
