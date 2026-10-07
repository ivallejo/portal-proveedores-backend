using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Ports.Outbound.Persistence;
using WebProveedores.Domain.Documents;

namespace WebProveedores.Infrastructure.Persistence;

public sealed class EfDocumentRepository(AppDbContext db) : IDocumentRepository
{
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

    public void Add(SupplierDocument document) => db.Documents.Add(document);
}
