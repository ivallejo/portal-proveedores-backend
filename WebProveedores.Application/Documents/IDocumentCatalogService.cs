using WebProveedores.Application.Documents.Commands;
using WebProveedores.Application.Documents.Responses;

namespace WebProveedores.Application.Documents;

/// <summary>Sociedades del usuario, áreas con sus aprobadores y validación de órdenes en SAP.</summary>
public interface IDocumentCatalogService
{
    Task<IReadOnlyList<CompanyResponse>> ListCompaniesAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AreaResponse>> ListAreasAsync(CancellationToken cancellationToken);
    Task<OrderValidationResponse?> ValidateOrderAsync(Guid userId, ValidateOrderCommand request, CancellationToken cancellationToken);
}
