using Microsoft.Extensions.Logging;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Application.Documents.Commands;
using WebProveedores.Application.Ports.Outbound.Files;
using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Documents;

/// <summary>Validación de los archivos recibidos y su guardado como adjuntos del documento.</summary>
internal sealed class DocumentFiles(IFileStorage storage, IPdfMerger pdfMerger, TimeProvider clock, ILogger<DocumentFiles> logger)
{
    public const long MaxFileBytes = 5 * 1024 * 1024;

    public static async Task<byte[]> ReadRequiredAsync(UploadedFile? file, string label, string[] extensions, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0) throw new ValidationException($"Adjunta el {label}.");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!extensions.Contains(extension))
            throw new ValidationException($"El {label} debe ser {string.Join(" o ", extensions)}. Recibimos «{Path.GetFileName(file.FileName)}».");
        if (file.Length > MaxFileBytes) throw new ValidationException($"El {label} supera los 5 MB permitidos.");

        using var buffer = new MemoryStream();
        await file.Content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        if (bytes.Length > MaxFileBytes) throw new ValidationException($"El {label} supera los 5 MB permitidos.");
        if (!HasExpectedSignature(extension, bytes))
            throw new ValidationException($"El contenido del {label} no corresponde a un archivo {extension}.");
        return bytes;
    }

    /// <summary>Los PDF de sustento se consolidan en un solo archivo.</summary>
    public byte[] MergePdfs(IReadOnlyList<byte[]> pdfs)
    {
        try { return pdfMerger.Merge(pdfs); }
        catch (InvalidDataException exception) { throw new ValidationException(exception.Message); }
    }

    public async Task AttachAsync(SupplierDocument document, AttachmentKind kind, UploadedFile file, byte[] bytes, List<string> stored, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var key = await storage.SaveAsync(new MemoryStream(bytes), extension, cancellationToken);
        stored.Add(key);
        var contentType = extension switch { ".pdf" => "application/pdf", ".xml" => "application/xml", ".zip" => "application/zip", _ => "application/octet-stream" };
        document.AddAttachment(kind, SafeFileName(file.FileName), key, contentType, bytes.Length, clock.GetUtcNow().UtcDateTime);
    }

    /// <summary>Si el registro falla, se borran los archivos ya guardados.</summary>
    public async Task DiscardAsync(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            try { await storage.DeleteAsync(key, CancellationToken.None); }
            catch (Exception exception) { logger.LogWarning(exception, "No se pudo eliminar el archivo temporal {StorageKey}", key); }
        }
    }

    /// <summary>Verifica la firma del archivo para no confiar solo en la extensión.</summary>
    private static bool HasExpectedSignature(string extension, byte[] bytes) => extension switch
    {
        ".pdf" => bytes.AsSpan().StartsWith("%PDF"u8),
        ".zip" => bytes.AsSpan().StartsWith("PK"u8),
        ".xml" => System.Text.Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 512)).TrimStart('﻿', ' ', '\r', '\n', '\t').StartsWith('<'),
        _ => false,
    };

    private static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return clean.Length > 200 ? clean[^200..] : clean;
    }
}
