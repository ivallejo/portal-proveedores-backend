using WebProveedores.Application.Catalog.Responses;
using WebProveedores.Application.Common.Access;
using WebProveedores.Application.Ports.Outbound.Persistence;

namespace WebProveedores.Application.Catalog;

internal sealed class CatalogService(IApproverDirectory approverDirectory, ICompanyReader companyReader, PortalActors actors) : ICatalogService
{
    /// <summary>Sociedades con las que trabaja el usuario (el administrador ve todas).</summary>
    public async Task<IReadOnlyList<CompanyResponse>> ListCompaniesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var actor = await actors.LoadAsync(userId, cancellationToken);
        return (await companyReader.ListActiveAsync(cancellationToken))
            .Where(company => actor.HasCompany(company.Id))
            .Select(company => new CompanyResponse(company.Code, company.Name, company.Ruc, company.BillingEmail))
            .ToArray();
    }

    public async Task<IReadOnlyList<AreaResponse>> ListAreasAsync(CancellationToken cancellationToken)
    {
        var approvers = await approverDirectory.ListApproversAsync(cancellationToken);
        return approvers
            .GroupBy(approver => (approver.AreaId, approver.AreaName, approver.AreaCompanyCode))
            .OrderBy(group => group.Key.AreaName)
            .Select(group => new AreaResponse(
                group.Key.AreaId,
                group.Key.AreaName,
                group.Key.AreaCompanyCode,
                group.OrderBy(item => item.Name).Select(item => new ApproverResponse(item.UserId, item.Name, item.Email, item.CompanyCodes)).ToArray()))
            .ToArray();
    }
}
