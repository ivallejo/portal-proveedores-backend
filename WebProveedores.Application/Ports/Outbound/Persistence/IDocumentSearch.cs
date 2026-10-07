using WebProveedores.Application.Ports.Outbound.Persistence.Models;

namespace WebProveedores.Application.Ports.Outbound.Persistence;

/// <summary>Bandejas de documentos: página filtrada y totales por estado (sin seguimiento).</summary>
public interface IDocumentSearch
{
    Task<DocumentPage> SearchAsync(DocumentQuery query, CancellationToken cancellationToken);
}
