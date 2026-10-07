using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Abstractions.Documents;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Domain.Access;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Organization;

namespace WebProveedores.Infrastructure.Persistence;

public sealed class EfDocumentRepository(AppDbContext db) : IDocumentRepository
{
    private static readonly DocumentStatus[] ApprovalStatuses =
        [DocumentStatus.PendingApproval, DocumentStatus.Approved, DocumentStatus.PendingAccounting, DocumentStatus.Rejected];

    private static readonly DocumentStatus[] AccountingStatuses =
        [DocumentStatus.PendingAccounting, DocumentStatus.Accounted, DocumentStatus.Observed];

    public Task<Company?> FindCompanyAsync(string code, CancellationToken cancellationToken) =>
        db.Companies.SingleOrDefaultAsync(company => company.Code == code, cancellationToken);

    public async Task<IReadOnlyList<Company>> ListCompaniesAsync(CancellationToken cancellationToken) =>
        await db.Companies.AsNoTracking().Where(company => company.IsActive).OrderBy(company => company.Code).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ApproverRecord>> ListApproversAsync(CancellationToken cancellationToken) =>
        await Approvers(null).ToListAsync(cancellationToken);

    public Task<ApproverRecord?> FindApproverAsync(Guid userId, CancellationToken cancellationToken) =>
        Approvers(userId).SingleOrDefaultAsync(cancellationToken);

    public Task<bool> ExistsAsync(string providerRuc, string number, CancellationToken cancellationToken) =>
        db.Documents.AnyAsync(document => document.ProviderRuc == providerRuc && document.Number == number, cancellationToken);

    public Task<SupplierDocument?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Documents
            .Include(document => document.Company)
            .Include(document => document.Items)
            .Include(document => document.Attachments)
            .Include(document => document.Events)
            .AsSplitQuery()
            .SingleOrDefaultAsync(document => document.Id == id, cancellationToken);

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

    public void Add(SupplierDocument document) => db.Documents.Add(document);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // Dos registros simultáneos del mismo comprobante: el índice único gana.
            throw new DocumentRejectedException("El documento ya fue registrado para ese RUC.");
        }
    }

    // El filtro por usuario va antes de la proyección para que SQL Server pueda traducirlo.
    private IQueryable<ApproverRecord> Approvers(Guid? userId) =>
        from user in db.Users
        where (userId == null || user.Id == userId)
              && user.IsActive
              && user.AreaId != null
              && user.Ruc == null
              // Aprobador: su rol (que no sea el de administrador) tiene la opción Documentos.
              && user.UserRoles.Any(userRole => userRole.Role.IsActive && userRole.Role.Code != SecurityCatalog.AdministratorRole
                  && userRole.Role.RoleMenus.Any(item => item.MenuOption.Code == MenuCatalog.Documents && item.MenuOption.IsActive))
        orderby user.CompanyName
        select new ApproverRecord(
            user.Id,
            user.CompanyName,
            user.Emails.Where(email => email.IsActive).OrderByDescending(email => email.IsPrimary).Select(email => email.Email).FirstOrDefault() ?? string.Empty,
            user.AreaId!.Value,
            user.Area!.Name,
            user.Area.Company.Code,
            user.UserCompanies.Select(userCompany => userCompany.Company.Code).ToList());

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains("IX_Documents_ProviderRuc_Number", StringComparison.Ordinal) == true;
}
