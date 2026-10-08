using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Application.Documents.Responses;
using WebProveedores.Application.Ports.Outbound.Files;
using WebProveedores.Application.Ports.Outbound.Persistence;
using WebProveedores.Application.Ports.Outbound.Persistence.Models;
using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Documents;

internal sealed class DocumentQueryService(IDocumentSearch documents, DocumentAccess access, IFileStorage storage) : IDocumentQueryService
{
    public async Task<DocumentPageResponse> SearchAsync(Guid userId, DocumentInbox inbox, string? providerRuc, DocumentStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var actor = await access.LoadActorAsync(userId, cancellationToken);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = new DocumentQuery(inbox, string.IsNullOrWhiteSpace(providerRuc) ? null : providerRuc.Trim(), status, page, pageSize);

        query = inbox switch
        {
            DocumentInbox.Approvals when actor.IsAdmin => query,
            DocumentInbox.Approvals when actor.IsApprover => query with { ApproverId = actor.Id, ApproverAreaId = actor.AreaId, CompanyIds = actor.CompanyIds },
            DocumentInbox.Accounting when actor.IsAdmin => query,
            DocumentInbox.Accounting when actor.IsAccounting => query with { CompanyIds = actor.CompanyIds },
            DocumentInbox.Mine when actor.IsProvider && !actor.IsAdmin => query with { OwnerRuc = actor.Ruc },
            DocumentInbox.Mine when !actor.IsAdmin => query with { RegisteredById = actor.Id },
            DocumentInbox.Mine => query,
            _ => throw new ForbiddenException("No tienes acceso a esta bandeja."),
        };

        var result = await documents.SearchAsync(query, cancellationToken);
        return new DocumentPageResponse(result.Items.Select(DocumentMapper.ToSummary).ToArray(), result.Total, page, pageSize, result.CountsByStatus);
    }

    public async Task<DocumentDetailResponse> GetAsync(Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        var (_, document) = await access.LoadVisibleAsync(userId, documentId, cancellationToken);
        return DocumentMapper.ToDetail(document);
    }

    public async Task<AttachmentContent> OpenAttachmentAsync(Guid userId, Guid documentId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var (_, document) = await access.LoadVisibleAsync(userId, documentId, cancellationToken);
        var attachment = document.Attachments.SingleOrDefault(item => item.Id == attachmentId)
            ?? throw new NotFoundException("El archivo no existe.");
        var content = await storage.OpenReadAsync(attachment.StorageKey, cancellationToken);
        return new AttachmentContent(content, attachment.FileName, attachment.ContentType);
    }
}
