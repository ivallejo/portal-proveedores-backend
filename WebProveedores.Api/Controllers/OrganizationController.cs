using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebProveedores.Api.Infrastructure;
using WebProveedores.Application.Organization;

namespace WebProveedores.Api.Controllers;

/// <summary>Configuración › Sociedades y Áreas (solo administrador).</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = Policies.UsersManage)]
public sealed class OrganizationController(IOrganizationService organization) : ControllerBase
{
    [HttpGet("companies")]
    public async Task<ActionResult<IReadOnlyList<CompanyAdminResponse>>> Companies([FromQuery] string? search, [FromQuery] bool? active, CancellationToken cancellationToken) =>
        Ok(await organization.ListCompaniesAsync(search, active, cancellationToken));

    [HttpPost("companies")]
    public async Task<ActionResult<CompanyAdminResponse>> CreateCompany(CompanyRequest request, CancellationToken cancellationToken) =>
        Ok(await organization.CreateCompanyAsync(request, cancellationToken));

    [HttpPut("companies/{id:guid}")]
    public async Task<ActionResult<CompanyAdminResponse>> UpdateCompany(Guid id, CompanyRequest request, CancellationToken cancellationToken) =>
        Ok(await organization.UpdateCompanyAsync(id, request, cancellationToken));

    [HttpPatch("companies/{id:guid}/status")]
    public async Task<ActionResult<CompanyAdminResponse>> SetCompanyStatus(Guid id, StatusRequest request, CancellationToken cancellationToken) =>
        Ok(await organization.SetCompanyStatusAsync(id, request.IsActive, cancellationToken));

    [HttpGet("areas")]
    public async Task<ActionResult<IReadOnlyList<AreaAdminResponse>>> Areas([FromQuery] string? search, [FromQuery] bool? active, [FromQuery] Guid? companyId, CancellationToken cancellationToken) =>
        Ok(await organization.ListAreasAsync(search, active, companyId, cancellationToken));

    [HttpPost("areas")]
    public async Task<ActionResult<AreaAdminResponse>> CreateArea(AreaRequest request, CancellationToken cancellationToken) =>
        Ok(await organization.CreateAreaAsync(request, cancellationToken));

    [HttpPut("areas/{id:guid}")]
    public async Task<ActionResult<AreaAdminResponse>> UpdateArea(Guid id, AreaRequest request, CancellationToken cancellationToken) =>
        Ok(await organization.UpdateAreaAsync(id, request, cancellationToken));

    [HttpPatch("areas/{id:guid}/status")]
    public async Task<ActionResult<AreaAdminResponse>> SetAreaStatus(Guid id, StatusRequest request, CancellationToken cancellationToken) =>
        Ok(await organization.SetAreaStatusAsync(id, request.IsActive, cancellationToken));
}
