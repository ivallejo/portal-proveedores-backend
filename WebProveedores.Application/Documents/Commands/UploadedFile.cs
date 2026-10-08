namespace WebProveedores.Application.Documents.Commands;

/// <summary>Archivo recibido en la petición, independiente de ASP.NET.</summary>
public sealed record UploadedFile(string FileName, string ContentType, long Length, Stream Content);
