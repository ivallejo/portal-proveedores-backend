namespace WebProveedores.Application.Contracts.Documents.Commands;

public sealed record RejectDocumentCommand
{
    public string Reason { get; init; } = string.Empty;
}
