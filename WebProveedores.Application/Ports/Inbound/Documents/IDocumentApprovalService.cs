using WebProveedores.Application.Contracts.Documents.Requests;
using WebProveedores.Application.Contracts.Documents.Responses;

namespace WebProveedores.Application.Ports.Inbound.Documents;

/// <summary>Decisión del aprobador de área.</summary>
public interface IDocumentApprovalService
{
    Task<DocumentDetailResponse> ApproveAsync(Guid userId, Guid documentId, ApproveDocumentRequest request, CancellationToken cancellationToken);
    Task<DocumentDetailResponse> RejectAsync(Guid userId, Guid documentId, RejectDocumentRequest request, CancellationToken cancellationToken);
    Task<DocumentDetailResponse> ReassignAsync(Guid userId, Guid documentId, ReassignDocumentRequest request, CancellationToken cancellationToken);
}
