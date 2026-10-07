using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Ports.Outbound.Persistence;

/// <summary>Documentos con seguimiento de cambios, para registrarlos o cambiar su estado y guardar con <see cref="IUnitOfWork"/>.</summary>
public interface IDocumentRepository
{
    Task<bool> ExistsAsync(string providerRuc, string number, CancellationToken cancellationToken);
    Task<SupplierDocument?> FindAsync(Guid id, CancellationToken cancellationToken);
    void Add(SupplierDocument document);
}
