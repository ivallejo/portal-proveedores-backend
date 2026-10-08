using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Contracts.Documents.Responses;

public sealed record DocumentEventResponse(string Title, string Actor, DocumentEventKind Kind, DateTime OccurredAtUtc, string? Note);
