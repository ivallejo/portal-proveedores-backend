using WebProveedores.Application.Ports.Outbound.Persistence.Models;
using WebProveedores.Domain.Organization;

namespace WebProveedores.Application.Ports.Outbound.Persistence;

/// <summary>Sociedades y áreas con sus totales (con seguimiento, para editarlas o asignarlas).</summary>
public interface IOrganizationReader
{
    Task<IReadOnlyList<CompanySummary>> ListCompaniesAsync(CancellationToken cancellationToken);
    Task<Company?> FindCompanyAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<AreaSummary>> ListAreasAsync(CancellationToken cancellationToken);
    Task<Area?> FindAreaAsync(Guid id, CancellationToken cancellationToken);
}
