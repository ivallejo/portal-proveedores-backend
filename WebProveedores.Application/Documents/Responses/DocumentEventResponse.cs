using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Documents.Responses;

public sealed record DocumentEventResponse(string Title, string Actor, DocumentEventKind Kind, DateTime OccurredAtUtc, string? Note);
