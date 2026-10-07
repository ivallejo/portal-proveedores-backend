using WebProveedores.Domain.Organization;

namespace WebProveedores.Application.Ports.Outbound.Persistence;

/// <summary>Alta de sociedades y áreas, y los datos que no pueden repetirse entre ellas.</summary>
public interface IOrganizationRepository
{
    Task<bool> CompanyCodeExistsAsync(string code, Guid? exceptId, CancellationToken cancellationToken);
    Task<bool> CompanyRucExistsAsync(string ruc, Guid? exceptId, CancellationToken cancellationToken);
    void AddCompany(Company company);

    Task<bool> AreaCodeExistsAsync(Guid companyId, string code, Guid? exceptId, CancellationToken cancellationToken);
    void AddArea(Area area);
}
