using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Ports.Outbound.Persistence.Models;

public sealed record DocumentQuery(
    DocumentInbox Inbox,
    string? ProviderRuc,
    DocumentStatus? Status,
    int Page,
    int PageSize,
    // Si se indica, limita la bandeja de aprobación a ese aprobador o a su área.
    Guid? ApproverId = null,
    Guid? ApproverAreaId = null,
    // Si se indica, limita «Mine» a ese RUC emisor.
    string? OwnerRuc = null,
    // Si se indica, limita «Mine» a lo registrado por ese usuario.
    Guid? RegisteredById = null,
    // Si se indica, limita la bandeja a documentos de esas sociedades.
    IReadOnlyCollection<Guid>? CompanyIds = null);
