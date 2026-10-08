using WebProveedores.Application.Contracts.Documents.Responses;
using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Ports.Inbound.Documents;

/// <summary>Bandejas, detalle y descarga de adjuntos.</summary>
public interface IDocumentQueryService
{
    Task<DocumentPageResponse> SearchAsync(Guid userId, DocumentInbox inbox, string? providerRuc, DocumentStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<DocumentDetailResponse> GetAsync(Guid userId, Guid documentId, CancellationToken cancellationToken);
    Task<AttachmentContent> OpenAttachmentAsync(Guid userId, Guid documentId, Guid attachmentId, CancellationToken cancellationToken);
}
