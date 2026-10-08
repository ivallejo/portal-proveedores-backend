using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Documents.Responses;

public sealed record AttachmentResponse(Guid Id, AttachmentKind Kind, string FileName, long SizeBytes);
