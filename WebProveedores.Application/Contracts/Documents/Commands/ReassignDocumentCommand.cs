namespace WebProveedores.Application.Contracts.Documents.Commands;

public sealed record ReassignDocumentCommand
{
    public Guid ApproverId { get; init; }
    public string Reason { get; init; } = string.Empty;
}
