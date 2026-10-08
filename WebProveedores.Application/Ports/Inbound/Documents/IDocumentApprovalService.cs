using WebProveedores.Application.Contracts.Documents.Commands;
using WebProveedores.Application.Contracts.Documents.Responses;

namespace WebProveedores.Application.Ports.Inbound.Documents;

/// <summary>Decisión del aprobador de área.</summary>
public interface IDocumentApprovalService
{
    Task<DocumentDetailResponse> ApproveAsync(Guid userId, Guid documentId, ApproveDocumentCommand request, CancellationToken cancellationToken);
    Task<DocumentDetailResponse> RejectAsync(Guid userId, Guid documentId, RejectDocumentCommand request, CancellationToken cancellationToken);
    Task<DocumentDetailResponse> ReassignAsync(Guid userId, Guid documentId, ReassignDocumentCommand request, CancellationToken cancellationToken);
}
