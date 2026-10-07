using WebProveedores.Application.Ports.Outbound.Persistence.Models;
using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Documents;

/// <summary>Sociedades del usuario, áreas con sus aprobadores y validación de órdenes en SAP.</summary>
public interface IDocumentCatalogService
{
    Task<IReadOnlyList<CompanyResponse>> ListCompaniesAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AreaResponse>> ListAreasAsync(CancellationToken cancellationToken);
    Task<OrderValidationResponse?> ValidateOrderAsync(Guid userId, ValidateOrderRequest request, CancellationToken cancellationToken);
}

/// <summary>Registro de comprobantes electrónicos (Con OC / Sin OC) y documentos especiales.</summary>
public interface IDocumentRegistrationService
{
    Task<DocumentDetailResponse> RegisterAsync(Guid userId, RegisterElectronicDocumentCommand command, CancellationToken cancellationToken);
    Task<DocumentDetailResponse> RegisterSpecialAsync(Guid userId, RegisterSpecialDocumentCommand command, CancellationToken cancellationToken);
}

/// <summary>Bandejas, detalle y descarga de adjuntos.</summary>
public interface IDocumentQueryService
{
    Task<DocumentPageResponse> SearchAsync(Guid userId, DocumentInbox inbox, string? providerRuc, DocumentStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<DocumentDetailResponse> GetAsync(Guid userId, Guid documentId, CancellationToken cancellationToken);
    Task<AttachmentContent> OpenAttachmentAsync(Guid userId, Guid documentId, Guid attachmentId, CancellationToken cancellationToken);
}

/// <summary>Decisión del aprobador de área.</summary>
public interface IDocumentApprovalService
{
    Task<DocumentDetailResponse> ApproveAsync(Guid userId, Guid documentId, ApproveDocumentRequest request, CancellationToken cancellationToken);
    Task<DocumentDetailResponse> RejectAsync(Guid userId, Guid documentId, RejectDocumentRequest request, CancellationToken cancellationToken);
    Task<DocumentDetailResponse> ReassignAsync(Guid userId, Guid documentId, ReassignDocumentRequest request, CancellationToken cancellationToken);
}

/// <summary>Revisión de Cuentas por pagar.</summary>
public interface IDocumentAccountingService
{
    Task<DocumentDetailResponse> RejectAsync(Guid userId, Guid documentId, RejectDocumentRequest request, CancellationToken cancellationToken);
    Task<DocumentDetailResponse> ObserveAsync(Guid userId, Guid documentId, ObserveDocumentRequest request, CancellationToken cancellationToken);
}
