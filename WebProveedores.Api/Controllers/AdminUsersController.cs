using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebProveedores.Application.Admin;

namespace WebProveedores.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = "Users.Manage")]
public sealed class AdminUsersController(IAdminUserService users) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminUserResponse>>> List(CancellationToken cancellationToken) => Ok(await users.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<AdminUserResponse>> Create(CreateUserRequest request, CancellationToken cancellationToken)
        => Ok(await users.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}/role")]
    public async Task<ActionResult<AdminUserResponse>> AssignRole(Guid id, AssignRoleRequest request, CancellationToken cancellationToken)
        => await users.AssignRoleAsync(id, request, cancellationToken) is { } result ? Ok(result) : NotFound();

    [HttpPut("{id:guid}/companies")]
    public async Task<ActionResult<AdminUserResponse>> AssignCompanies(Guid id, AssignCompaniesRequest request, CancellationToken cancellationToken)
        => await users.AssignCompaniesAsync(id, request, cancellationToken) is { } result ? Ok(result) : NotFound();

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<AdminUserResponse>> SetStatus(Guid id, UpdateUserStatusRequest request, CancellationToken cancellationToken) => await users.SetStatusAsync(id, request, cancellationToken) is { } result ? Ok(result) : NotFound();
}
