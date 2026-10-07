using WebProveedores.Application.Ports.Outbound.Persistence.Models;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Organization;

namespace WebProveedores.Application.Ports.Outbound.Persistence;

public interface IDocumentRepository
{
    Task<Company?> FindCompanyAsync(string code, CancellationToken cancellationToken);
    Task<IReadOnlyList<Company>> ListCompaniesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ApproverRecord>> ListApproversAsync(CancellationToken cancellationToken);
    Task<ApproverRecord?> FindApproverAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string providerRuc, string number, CancellationToken cancellationToken);
    Task<SupplierDocument?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<DocumentPage> SearchAsync(DocumentQuery query, CancellationToken cancellationToken);
    void Add(SupplierDocument document);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
