using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Contracts.Documents.Commands;

public sealed record RegisterSpecialDocumentCommand(
    string CompanyCode,
    SpecialDocumentType DocumentType,
    string ProviderRuc,
    DateOnly IssuedAt,
    string Number,
    decimal Amount,
    Currency Currency,
    UploadedFile? Pdf);
