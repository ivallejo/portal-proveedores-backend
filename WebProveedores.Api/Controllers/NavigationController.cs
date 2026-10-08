using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebProveedores.Api.Security;
using WebProveedores.Application.Contracts.Access.Responses;
using WebProveedores.Application.Ports.Inbound.Access;

namespace WebProveedores.Api.Controllers;

/// <summary>Menú de quien tiene sesión.</summary>
[ApiController]
[Route("api/navigation")]
[Authorize]
public sealed class NavigationController(INavigationService navigation, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NavigationItem>>> Get(CancellationToken cancellationToken) =>
        Ok(await navigation.MenuForAsync(currentUser.Id, cancellationToken));
}
