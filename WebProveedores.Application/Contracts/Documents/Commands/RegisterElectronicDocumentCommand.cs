using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Contracts.Documents.Commands;

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
