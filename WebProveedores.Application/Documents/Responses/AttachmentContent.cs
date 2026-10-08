namespace WebProveedores.Application.Documents.Responses;

public sealed record AttachmentContent(Stream Content, string FileName, string ContentType);
