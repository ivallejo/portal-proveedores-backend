using WebProveedores.Domain.Organization;

namespace WebProveedores.Application.Ports.Outbound.Persistence;

/// <summary>Alta de sociedades y áreas, y los datos que no pueden repetirse entre ellas.</summary>
public interface IOrganizationRepository
{
    Task<bool> CompanyCodeExistsAsync(string code, Guid? exceptId, CancellationToken cancellationToken);
    Task<bool> CompanyRucExistsAsync(string ruc, Guid? exceptId, CancellationToken cancellationToken);
    void AddCompany(Company company);

    /// <summary>La clave del área es única en todo el portal.</summary>
    Task<bool> AreaCodeExistsAsync(string code, Guid? exceptId, CancellationToken cancellationToken);
    /// <summary>Usuarios del área que no tienen ninguna de estas sociedades (quedarían fuera al quitar las demás).</summary>
    Task<IReadOnlyList<string>> AreaUsersOutsideCompaniesAsync(Guid areaId, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken);
    /// <summary>Documentos del área pendientes de aprobación en estas sociedades.</summary>
    Task<int> PendingApprovalsAsync(Guid areaId, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken);
    void AddArea(Area area);
}
