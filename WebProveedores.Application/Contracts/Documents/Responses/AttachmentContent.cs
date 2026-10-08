namespace WebProveedores.Application.Contracts.Documents.Responses;

public sealed record AttachmentContent(Stream Content, string FileName, string ContentType);
