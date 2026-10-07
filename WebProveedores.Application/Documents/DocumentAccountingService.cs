using WebProveedores.Application.Ports.Outbound.Persistence;
using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Documents;

internal sealed class DocumentAccountingService(IDocumentRepository documents, DocumentAccess access, DocumentNotifier notifier, TimeProvider clock) : IDocumentAccountingService
{
    public async Task<DocumentDetailResponse> RejectAsync(Guid userId, Guid documentId, RejectDocumentRequest request, CancellationToken cancellationToken)
    {
        var (actor, document) = await access.LoadForAccountingAsync(userId, documentId, "Solo Cuentas por pagar puede rechazar en contabilización.", cancellationToken);
        document.Reject(RejectionStage.Accounting, request.Reason, actor.Label, clock.GetUtcNow().UtcDateTime);
        await documents.SaveChangesAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(document.ProviderEmail))
            await notifier.RejectedAsync(document.ProviderEmail, document, request.Reason.Trim(), cancellationToken);
        return DocumentMapper.ToDetail(document);
    }

    public async Task<DocumentDetailResponse> ObserveAsync(Guid userId, Guid documentId, ObserveDocumentRequest request, CancellationToken cancellationToken)
    {
        var (actor, document) = await access.LoadForAccountingAsync(userId, documentId, "Solo Cuentas por pagar puede observar documentos.", cancellationToken);
        var email = request.Email.Trim().ToLowerInvariant();
        document.Observe(request.Reason, email, actor.Label, clock.GetUtcNow().UtcDateTime);
        await documents.SaveChangesAsync(cancellationToken);
        await notifier.ObservedAsync(email, document, request.Reason.Trim(), cancellationToken);
        return DocumentMapper.ToDetail(document);
    }
}
