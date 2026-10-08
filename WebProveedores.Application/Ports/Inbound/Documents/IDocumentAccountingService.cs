using WebProveedores.Application.Contracts.Documents.Commands;
using WebProveedores.Application.Contracts.Documents.Responses;

namespace WebProveedores.Application.Ports.Inbound.Documents;

/// <summary>Revisión de Cuentas por pagar.</summary>
public interface IDocumentAccountingService
{
    Task<DocumentDetailResponse> RejectAsync(Guid userId, Guid documentId, RejectDocumentCommand request, CancellationToken cancellationToken);
    Task<DocumentDetailResponse> ObserveAsync(Guid userId, Guid documentId, ObserveDocumentCommand request, CancellationToken cancellationToken);
}
