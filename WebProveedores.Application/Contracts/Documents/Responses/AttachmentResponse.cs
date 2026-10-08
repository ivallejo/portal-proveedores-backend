using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Contracts.Documents.Responses;

public sealed record AttachmentResponse(Guid Id, AttachmentKind Kind, string FileName, long SizeBytes);
