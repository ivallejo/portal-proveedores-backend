using WebProveedores.Application.Abstractions.Documents;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Organization;

namespace WebProveedores.Application.Documents;

internal sealed class DocumentApprovalService(IDocumentRepository documents, DocumentAccess access, DocumentNotifier notifier, TimeProvider clock) : IDocumentApprovalService
{
    public async Task<DocumentDetailResponse> ApproveAsync(Guid userId, Guid documentId, ApproveDocumentRequest request, CancellationToken cancellationToken)
    {
        var (actor, document) = await access.LoadForApprovalAsync(userId, documentId, cancellationToken);
        document.Approve(request.ReferenceType, request.Reference, actor.Label, clock.GetUtcNow().UtcDateTime);
        await documents.SaveChangesAsync(cancellationToken);
        return DocumentMapper.ToDetail(document);
    }

    public async Task<DocumentDetailResponse> RejectAsync(Guid userId, Guid documentId, RejectDocumentRequest request, CancellationToken cancellationToken)
    {
        var (actor, document) = await access.LoadForApprovalAsync(userId, documentId, cancellationToken);
        document.Reject(RejectionStage.Approver, request.Reason, actor.Label, clock.GetUtcNow().UtcDateTime);
        await documents.SaveChangesAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(document.ProviderEmail))
            await notifier.RejectedAsync(document.ProviderEmail, document, request.Reason.Trim(), cancellationToken);
        return DocumentMapper.ToDetail(document);
    }

    public async Task<DocumentDetailResponse> ReassignAsync(Guid userId, Guid documentId, ReassignDocumentRequest request, CancellationToken cancellationToken)
    {
        var (actor, document) = await access.LoadForApprovalAsync(userId, documentId, cancellationToken);
        var approver = await access.RequireApproverAsync(request.ApproverId, document.Company, cancellationToken);
        document.Reassign(new ApproverAssignment(approver.AreaId, approver.AreaName, approver.UserId, approver.Name, approver.Email), request.Reason, actor.Label, clock.GetUtcNow().UtcDateTime);
        await documents.SaveChangesAsync(cancellationToken);
        await notifier.PendingApprovalAsync(approver.Email, approver.Name, document, document.Company.Name, cancellationToken, request.Reason.Trim());
        return DocumentMapper.ToDetail(document);
    }
}
