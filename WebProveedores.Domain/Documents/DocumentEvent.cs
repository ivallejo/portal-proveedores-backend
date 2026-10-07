namespace WebProveedores.Domain.Documents;

public sealed class DocumentEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid DocumentId { get; set; }
    public int Sequence { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DocumentEventKind Kind { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? Note { get; set; }
}
