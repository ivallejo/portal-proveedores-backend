namespace WebProveedores.Application.Documents.Commands;

public sealed record ObserveDocumentCommand
{
    public string Reason { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}
