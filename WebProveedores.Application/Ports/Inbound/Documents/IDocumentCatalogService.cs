using WebProveedores.Application.Contracts.Documents.Requests;
using WebProveedores.Application.Contracts.Documents.Responses;

namespace WebProveedores.Application.Ports.Inbound.Documents;

/// <summary>Sociedades del usuario, áreas con sus aprobadores y validación de órdenes en SAP.</summary>
public interface IDocumentCatalogService
{
    Task<IReadOnlyList<CompanyResponse>> ListCompaniesAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AreaResponse>> ListAreasAsync(CancellationToken cancellationToken);
    Task<OrderValidationResponse?> ValidateOrderAsync(Guid userId, ValidateOrderRequest request, CancellationToken cancellationToken);
}
