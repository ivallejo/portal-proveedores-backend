namespace WebProveedores.Application.Documents.Commands;

public sealed record ReassignDocumentCommand
{
    public Guid ApproverId { get; init; }
    public string Reason { get; init; } = string.Empty;
}
