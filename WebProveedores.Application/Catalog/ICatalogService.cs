using WebProveedores.Application.Catalog.Responses;

namespace WebProveedores.Application.Catalog;

/// <summary>Sociedades del usuario y áreas con sus aprobadores, para los filtros y formularios del portal.</summary>
public interface ICatalogService
{
    Task<IReadOnlyList<CompanyResponse>> ListCompaniesAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AreaResponse>> ListAreasAsync(CancellationToken cancellationToken);
}
