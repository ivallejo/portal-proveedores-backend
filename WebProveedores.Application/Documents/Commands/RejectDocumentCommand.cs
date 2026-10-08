namespace WebProveedores.Application.Documents.Commands;

public sealed record RejectDocumentCommand
{
    public string Reason { get; init; } = string.Empty;
}
