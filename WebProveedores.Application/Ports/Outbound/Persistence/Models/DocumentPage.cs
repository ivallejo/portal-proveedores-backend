using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Ports.Outbound.Persistence.Models;

public sealed record DocumentPage(
    IReadOnlyList<SupplierDocument> Items,
    int Total,
    IReadOnlyDictionary<DocumentStatus, int> CountsByStatus);
