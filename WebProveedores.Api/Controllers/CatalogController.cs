using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebProveedores.Api.Security;
using WebProveedores.Application.Documents;
using WebProveedores.Application.Documents.Responses;

namespace WebProveedores.Api.Controllers;

[ApiController]
[Route("api/catalog")]
[Authorize]
public sealed class CatalogController(IDocumentCatalogService catalog, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Sociedades del usuario en sesión (el administrador ve todas).</summary>
    [HttpGet("companies")]
    public async Task<ActionResult<IReadOnlyList<CompanyResponse>>> Companies(CancellationToken cancellationToken) =>
        Ok(await catalog.ListCompaniesAsync(currentUser.Id, cancellationToken));

    /// <summary>Áreas con sus aprobadores activos.</summary>
    [HttpGet("areas")]
    public async Task<ActionResult<IReadOnlyList<AreaResponse>>> Areas(CancellationToken cancellationToken) =>
        Ok(await catalog.ListAreasAsync(cancellationToken));
}
