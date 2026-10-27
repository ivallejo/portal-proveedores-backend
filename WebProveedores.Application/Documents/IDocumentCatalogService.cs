using WebProveedores.Application.Documents.Commands;
using WebProveedores.Application.Documents.Responses;

namespace WebProveedores.Application.Documents;

/// <summary>Validación de la orden de compra o servicio en SAP antes de registrar el documento.</summary>
public interface IDocumentCatalogService
{
    Task<OrderValidationResponse?> ValidateOrderAsync(Guid userId, ValidateOrderCommand request, CancellationToken cancellationToken);
}
