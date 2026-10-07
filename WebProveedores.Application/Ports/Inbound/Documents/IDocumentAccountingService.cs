using WebProveedores.Application.Documents;

namespace WebProveedores.Application.Ports.Inbound.Documents;

/// <summary>Revisión de Cuentas por pagar.</summary>
public interface IDocumentAccountingService
{
    Task<DocumentDetailResponse> RejectAsync(Guid userId, Guid documentId, RejectDocumentRequest request, CancellationToken cancellationToken);
    Task<DocumentDetailResponse> ObserveAsync(Guid userId, Guid documentId, ObserveDocumentRequest request, CancellationToken cancellationToken);
}
