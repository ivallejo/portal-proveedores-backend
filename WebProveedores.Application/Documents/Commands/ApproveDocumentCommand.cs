using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Documents.Commands;

public sealed record ApproveDocumentCommand
{
    public ApprovalReferenceType ReferenceType { get; init; }
    public string Reference { get; init; } = string.Empty;
}
