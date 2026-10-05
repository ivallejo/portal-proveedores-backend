using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebProveedores.Api.Infrastructure;
using WebProveedores.Application.Admin;

namespace WebProveedores.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = "Users.Manage")]
public sealed class AdminUsersController(IAdminUserService users, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AdminUserPage>> Search([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Ok(await users.SearchAsync(search, page, pageSize, cancellationToken));

    /// <summary>Roles, áreas y sociedades para los formularios.</summary>
    [HttpGet("catalog")]
    public async Task<ActionResult<AdminCatalogResponse>> Catalog(CancellationToken cancellationToken)
        => Ok(await users.CatalogAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<AdminUserResponse>> Create(CreateUserRequest request, CancellationToken cancellationToken)
        => Ok(await users.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminUserResponse>> Update(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
        => await users.UpdateAsync(UserId, id, request, cancellationToken) is { } result ? Ok(result) : NotFound();

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<AdminUserResponse>> SetStatus(Guid id, UpdateUserStatusRequest request, CancellationToken cancellationToken)
        => await users.SetStatusAsync(UserId, id, request, cancellationToken) is { } result ? Ok(result) : NotFound();

    /// <summary>Quita el bloqueo temporal por intentos fallidos.</summary>
    [HttpPost("{id:guid}/unlock")]
    public async Task<ActionResult<AdminUserResponse>> Unlock(Guid id, CancellationToken cancellationToken)
        => await users.UnlockAsync(id, cancellationToken) is { } result ? Ok(result) : NotFound();

    private Guid UserId => currentUser.Id;
}
