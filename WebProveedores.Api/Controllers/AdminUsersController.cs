using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebProveedores.Api.Infrastructure;
using WebProveedores.Application.Admin;
using WebProveedores.Application.Ports.Inbound.Admin;

namespace WebProveedores.Api.Controllers;

/// <summary>Configuración › Usuarios (solo administrador).</summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = Policies.UsersManage)]
public sealed class AdminUsersController(IAdminUserService users, ICurrentUser currentUser) : ControllerBase
{
    /// <summary><paramref name="status"/>: active, inactive o locked.</summary>
    [HttpGet]
    public async Task<ActionResult<AdminUserPage>> Search([FromQuery] string? search, [FromQuery] string? role, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
        => Ok(await users.SearchAsync(search, role, status, page, pageSize, cancellationToken));

    /// <summary>Roles, áreas y sociedades para los formularios.</summary>
    [HttpGet("catalog")]
    public async Task<ActionResult<AdminCatalogResponse>> Catalog(CancellationToken cancellationToken)
        => Ok(await users.CatalogAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminUserDetail>> Get(Guid id, CancellationToken cancellationToken)
        => await users.GetAsync(id, cancellationToken) is { } result ? Ok(result) : NotFound();

    /// <summary>Crea la cuenta y envía el enlace de activación al correo principal.</summary>
    [HttpPost]
    public async Task<ActionResult<AdminUserDetail>> Create(SaveUserRequest request, CancellationToken cancellationToken)
        => Ok(await users.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminUserDetail>> Update(Guid id, SaveUserRequest request, CancellationToken cancellationToken)
        => await users.UpdateAsync(currentUser.Id, id, request, cancellationToken) is { } result ? Ok(result) : NotFound();

    /// <summary>Activar también quita el bloqueo por intentos fallidos.</summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<AdminUserDetail>> SetStatus(Guid id, UpdateUserStatusRequest request, CancellationToken cancellationToken)
        => await users.SetStatusAsync(currentUser.Id, id, request, cancellationToken) is { } result ? Ok(result) : NotFound();

    /// <summary>Enlace de activación (si aún no activó la cuenta) o de recuperación de contraseña.</summary>
    [HttpPost("{id:guid}/password-link")]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<ActionResult<PasswordLinkSent>> SendPasswordLink(Guid id, CancellationToken cancellationToken)
        => await users.SendPasswordLinkAsync(id, cancellationToken) is { } result ? Ok(result) : NotFound();

    [HttpGet("{id:guid}/password-links")]
    public async Task<ActionResult<IReadOnlyList<PasswordLinkResponse>>> PasswordLinks(Guid id, CancellationToken cancellationToken)
        => await users.PasswordLinksAsync(id, cancellationToken) is { } result ? Ok(result) : NotFound();
}
