using WebProveedores.Application.Contracts.Organization.Requests;
using WebProveedores.Application.Contracts.Organization.Responses;

namespace WebProveedores.Application.Ports.Inbound.Organization;

/// <summary>Configuración de sociedades y áreas (solo administrador).</summary>
public interface IOrganizationService
{
    Task<IReadOnlyList<CompanyAdminResponse>> ListCompaniesAsync(string? search, bool? active, CancellationToken cancellationToken);
    Task<CompanyAdminResponse> CreateCompanyAsync(CompanyRequest request, CancellationToken cancellationToken);
    Task<CompanyAdminResponse> UpdateCompanyAsync(Guid id, CompanyRequest request, CancellationToken cancellationToken);
    Task<CompanyAdminResponse> SetCompanyStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken);

    Task<IReadOnlyList<AreaAdminResponse>> ListAreasAsync(string? search, bool? active, Guid? companyId, CancellationToken cancellationToken);
    Task<AreaAdminResponse> CreateAreaAsync(AreaRequest request, CancellationToken cancellationToken);
    Task<AreaAdminResponse> UpdateAreaAsync(Guid id, AreaRequest request, CancellationToken cancellationToken);
    Task<AreaAdminResponse> SetAreaStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken);
}
