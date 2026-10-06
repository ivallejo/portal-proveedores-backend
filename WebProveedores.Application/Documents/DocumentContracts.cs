using System.ComponentModel.DataAnnotations;
using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Documents;

/// <summary>Archivo recibido en la petición, independiente de ASP.NET.</summary>
public sealed record UploadedFile(string FileName, string ContentType, long Length, Stream Content);

public sealed record RegisterElectronicDocumentCommand(
    DocumentEntryType EntryType,
    string CompanyCode,
    bool IsPettyCash,
    OrderType? OrderType,
    string? OrderNumber,
    Guid? ApproverId,
    UploadedFile? Xml,
    UploadedFile? Pdf,
    UploadedFile? Cdr,
    IReadOnlyList<UploadedFile> Extras);

public enum SpecialDocumentType
{
    AirTicket,
    PublicReceipt,
    NonDomiciled,
    CollectionSettlement,
}

public sealed record RegisterSpecialDocumentCommand(
    string CompanyCode,
    SpecialDocumentType DocumentType,
    string ProviderRuc,
    DateOnly IssuedAt,
    string Number,
    decimal Amount,
    Currency Currency,
    UploadedFile? Pdf);

public sealed class ValidateOrderRequest
{
    [Required, MaxLength(20)] public string CompanyCode { get; init; } = string.Empty;
    [Required] public OrderType OrderType { get; init; }
    [Required, MaxLength(20)] public string Number { get; init; } = string.Empty;
}

public sealed class ApproveDocumentRequest
{
    [Required] public ApprovalReferenceType ReferenceType { get; init; }
    [Required, MaxLength(30)] public string Reference { get; init; } = string.Empty;
}

public sealed class RejectDocumentRequest
{
    [Required, MaxLength(1000)] public string Reason { get; init; } = string.Empty;
}

public sealed class ReassignDocumentRequest
{
    [Required] public Guid ApproverId { get; init; }
    [Required, MaxLength(1000)] public string Reason { get; init; } = string.Empty;
}

public sealed class ObserveDocumentRequest
{
    [Required, MaxLength(1000)] public string Reason { get; init; } = string.Empty;
    [Required, EmailAddress, MaxLength(320)] public string Email { get; init; } = string.Empty;
}

public sealed record OrderValidationResponse(string Number, OrderType OrderType, string Description, decimal Balance);

/// <param name="BillingEmail">Correo de facturación de la sociedad (recepción de comprobantes electrónicos).</param>
public sealed record CompanyResponse(string Code, string Name, string? Ruc, string? BillingEmail);

public sealed record AreaResponse(Guid Id, string Name, IReadOnlyList<ApproverResponse> Approvers);

/// <summary>Aprobador con las sociedades en las que puede aprobar (el frontend filtra por la sociedad del documento).</summary>
public sealed record ApproverResponse(Guid Id, string Name, string Email, IReadOnlyList<string> CompanyCodes);

public sealed record DocumentSummaryResponse(
    Guid Id,
    string Number,
    DocumentEntryType EntryType,
    string DocumentType,
    string ProviderRuc,
    string ProviderName,
    Currency Currency,
    decimal Amount,
    DocumentStatus Status,
    bool IsPettyCash,
    DateOnly IssuedAt,
    DateTime RegisteredAtUtc,
    string? ApproverName,
    string? OrderNumber);

public sealed record DocumentPageResponse(
    IReadOnlyList<DocumentSummaryResponse> Items,
    int Total,
    int Page,
    int PageSize,
    IReadOnlyDictionary<DocumentStatus, int> CountsByStatus);

public sealed record DocumentDetailResponse(
    Guid Id,
    string Number,
    DocumentEntryType EntryType,
    string DocumentType,
    string ProviderRuc,
    string ProviderName,
    string? ProviderEmail,
    Currency Currency,
    decimal Subtotal,
    decimal? Igv,
    decimal Amount,
    string Concept,
    DateOnly IssuedAt,
    DateTime RegisteredAtUtc,
    string RegisteredBy,
    CompanyResponse Company,
    DocumentStatus Status,
    bool IsPettyCash,
    RejectionStage? RejectedBy,
    string? AreaName,
    string? ApproverName,
    string? ApproverEmail,
    DateTime? ApprovedAtUtc,
    ApprovalReferenceType? ApprovalReferenceType,
    string? ApprovalReference,
    OrderType? OrderType,
    string? OrderNumber,
    decimal? OrderBalance,
    string? OrderDescription,
    string? Validation,
    IReadOnlyList<DocumentItemResponse> Items,
    IReadOnlyList<AttachmentResponse> Attachments,
    IReadOnlyList<DocumentEventResponse> History);

public sealed record DocumentItemResponse(string Description, decimal Quantity, decimal UnitPrice, decimal Amount);

public sealed record AttachmentResponse(Guid Id, AttachmentKind Kind, string FileName, long SizeBytes);

public sealed record DocumentEventResponse(string Title, string Actor, DocumentEventKind Kind, DateTime OccurredAtUtc, string? Note);

public sealed record AttachmentContent(Stream Content, string FileName, string ContentType);

/// <summary>El documento no superó una validación de negocio (SAP, SUNAT o duplicidad).</summary>
public sealed class DocumentRejectedException(string message) : Exception(message);
