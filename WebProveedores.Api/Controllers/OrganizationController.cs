using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebProveedores.Api.Infrastructure;
using WebProveedores.Application.Organization;
using WebProveedores.Application.Ports.Inbound.Organization;

namespace WebProveedores.Api.Controllers;

/// <summary>Configuración › Sociedades y Áreas (solo administrador).</summary>
[ApiController]
[Route("api/admin")]
[Authorize]
public sealed class OrganizationController(IOrganizationService organization) : ControllerBase
{
    [HttpGet("companies")]
    [Authorize(Policy = Policies.CompaniesManage)]
    public async Task<ActionResult<IReadOnlyList<CompanyAdminResponse>>> Companies([FromQuery] string? search, [FromQuery] bool? active, CancellationToken cancellationToken) =>
        Ok(await organization.ListCompaniesAsync(search, active, cancellationToken));

    [HttpPost("companies")]
    [Authorize(Policy = Policies.CompaniesManage)]
    public async Task<ActionResult<CompanyAdminResponse>> CreateCompany(CompanyRequest request, CancellationToken cancellationToken) =>
        Ok(await organization.CreateCompanyAsync(request, cancellationToken));

    [HttpPut("companies/{id:guid}")]
    [Authorize(Policy = Policies.CompaniesManage)]
    public async Task<ActionResult<CompanyAdminResponse>> UpdateCompany(Guid id, CompanyRequest request, CancellationToken cancellationToken) =>
        Ok(await organization.UpdateCompanyAsync(id, request, cancellationToken));

    [HttpPatch("companies/{id:guid}/status")]
    [Authorize(Policy = Policies.CompaniesManage)]
    public async Task<ActionResult<CompanyAdminResponse>> SetCompanyStatus(Guid id, StatusRequest request, CancellationToken cancellationToken) =>
        Ok(await organization.SetCompanyStatusAsync(id, request.IsActive, cancellationToken));

    [HttpGet("areas")]
    [Authorize(Policy = Policies.AreasManage)]
    public async Task<ActionResult<IReadOnlyList<AreaAdminResponse>>> Areas([FromQuery] string? search, [FromQuery] bool? active, [FromQuery] Guid? companyId, CancellationToken cancellationToken) =>
        Ok(await organization.ListAreasAsync(search, active, companyId, cancellationToken));

    [HttpPost("areas")]
    [Authorize(Policy = Policies.AreasManage)]
    public async Task<ActionResult<AreaAdminResponse>> CreateArea(AreaRequest request, CancellationToken cancellationToken) =>
        Ok(await organization.CreateAreaAsync(request, cancellationToken));

    [HttpPut("areas/{id:guid}")]
    [Authorize(Policy = Policies.AreasManage)]
    public async Task<ActionResult<AreaAdminResponse>> UpdateArea(Guid id, AreaRequest request, CancellationToken cancellationToken) =>
        Ok(await organization.UpdateAreaAsync(id, request, cancellationToken));

    [HttpPatch("areas/{id:guid}/status")]
    [Authorize(Policy = Policies.AreasManage)]
    public async Task<ActionResult<AreaAdminResponse>> SetAreaStatus(Guid id, StatusRequest request, CancellationToken cancellationToken) =>
        Ok(await organization.SetAreaStatusAsync(id, request.IsActive, cancellationToken));
}
