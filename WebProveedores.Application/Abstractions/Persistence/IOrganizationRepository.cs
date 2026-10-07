using WebProveedores.Domain.Organization;

namespace WebProveedores.Application.Abstractions.Persistence;

/// <summary>Sociedades y áreas para su administración (con seguimiento, para editarlas).</summary>
public interface IOrganizationRepository
{
    Task<IReadOnlyList<CompanySummary>> ListCompaniesAsync(CancellationToken cancellationToken);
    Task<Company?> FindCompanyAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> CompanyCodeExistsAsync(string code, Guid? exceptId, CancellationToken cancellationToken);
    Task<bool> CompanyRucExistsAsync(string ruc, Guid? exceptId, CancellationToken cancellationToken);
    void AddCompany(Company company);

    Task<IReadOnlyList<AreaSummary>> ListAreasAsync(CancellationToken cancellationToken);
    Task<Area?> FindAreaAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> AreaCodeExistsAsync(Guid companyId, string code, Guid? exceptId, CancellationToken cancellationToken);
    void AddArea(Area area);
}

/// <summary>Sociedad con cuántas áreas tiene y cuántos usuarios trabajan con ella.</summary>
public sealed record CompanySummary(Company Company, int AreaCount, int UserCount);

/// <summary>Área (con su sociedad) y cuántos usuarios pertenecen a ella.</summary>
public sealed record AreaSummary(Area Area, int UserCount);
