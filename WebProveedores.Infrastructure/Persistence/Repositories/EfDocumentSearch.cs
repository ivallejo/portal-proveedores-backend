using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Ports.Outbound.Persistence;
using WebProveedores.Application.Ports.Outbound.Persistence.Models;
using WebProveedores.Domain.Documents;

namespace WebProveedores.Infrastructure.Persistence.Repositories;

public sealed class EfDocumentSearch(AppDbContext db) : IDocumentSearch
{
    private static readonly DocumentStatus[] ApprovalStatuses =
        [DocumentStatus.PendingApproval, DocumentStatus.Approved, DocumentStatus.PendingAccounting, DocumentStatus.Rejected];

    private static readonly DocumentStatus[] AccountingStatuses =
        [DocumentStatus.PendingAccounting, DocumentStatus.Accounted, DocumentStatus.Observed];

    public async Task<DocumentPage> SearchAsync(DocumentQuery query, CancellationToken cancellationToken)
    {
        var scoped = query.Inbox switch
        {
            DocumentInbox.Approvals => db.Documents.Where(document =>
                document.ApproverId != null
                && ApprovalStatuses.Contains(document.Status)
                && !(document.Status == DocumentStatus.Rejected && document.RejectedBy == RejectionStage.Accounting)),
            DocumentInbox.Accounting => db.Documents.Where(document =>
                AccountingStatuses.Contains(document.Status)
                || (document.Status == DocumentStatus.Rejected && document.RejectedBy == RejectionStage.Accounting)),
            _ => db.Documents.AsQueryable(),
        };

        if (query.ApproverId is { } approverId)
        {
            var areaId = query.ApproverAreaId;
            scoped = scoped.Where(document => document.ApproverId == approverId || (areaId != null && document.AreaId == areaId));
        }
        if (query.CompanyIds is { } companyIds) scoped = scoped.Where(document => companyIds.Contains(document.CompanyId));
        if (query.OwnerRuc is { } ownerRuc) scoped = scoped.Where(document => document.ProviderRuc == ownerRuc);
        if (query.RegisteredById is { } registeredBy) scoped = scoped.Where(document => document.RegisteredById == registeredBy);
        if (query.ProviderRuc is { } ruc) scoped = scoped.Where(document => document.ProviderRuc.Contains(ruc));

        // Los indicadores se calculan sin el filtro de estado, como en la pantalla.
        var counts = await scoped.AsNoTracking()
            .GroupBy(document => document.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);

        if (query.Status is { } status) scoped = scoped.Where(document => document.Status == status);
        var total = await scoped.CountAsync(cancellationToken);
        var items = await scoped.AsNoTracking()
            .OrderByDescending(document => document.RegisteredAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new DocumentPage(items, total, counts);
    }
}
