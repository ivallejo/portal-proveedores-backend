using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Application.Ports.Outbound.Files;

namespace WebProveedores.Infrastructure.Files;

/// <summary>
/// Guarda los adjuntos en un contenedor privado de Azure Blob Storage (Azurite en desarrollo), con las mismas claves
/// que el disco local. Las descargas pasan por la API: los blobs no son públicos.
/// </summary>
public sealed class AzureBlobFileStorage(StorageSettings settings) : IFileStorage
{
    private readonly BlobContainerClient container = new(settings.ConnectionString, settings.Container);

    /// <summary>Crea el contenedor si no existe (al arrancar la API).</summary>
    public Task EnsureContainerAsync(CancellationToken cancellationToken) =>
        container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        var key = StorageKeys.New(extension, DateTime.UtcNow);
        await container.GetBlobClient(key).UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = ContentTypeOf(extension) },
            Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
        }, cancellationToken);
        return key;
    }

    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        if (!StorageKeys.IsValid(storageKey)) throw new ArgumentException("Clave de archivo no válida.");
        try
        {
            return await container.GetBlobClient(storageKey).OpenReadAsync(cancellationToken: cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            throw new NotFoundException("El archivo no está disponible.");
        }
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        if (!StorageKeys.IsValid(storageKey)) throw new ArgumentException("Clave de archivo no válida.");
        await container.GetBlobClient(storageKey).DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    /// <summary>Sube un archivo con una clave ya existente (migración desde el disco). false si ya estaba.</summary>
    public async Task<bool> UploadExistingAsync(string storageKey, Stream content, CancellationToken cancellationToken)
    {
        if (!StorageKeys.IsValid(storageKey)) throw new ArgumentException("Clave de archivo no válida.");
        var blob = container.GetBlobClient(storageKey);
        if (await blob.ExistsAsync(cancellationToken)) return false;
        await blob.UploadAsync(content, new BlobHttpHeaders { ContentType = ContentTypeOf(Path.GetExtension(storageKey)) }, cancellationToken: cancellationToken);
        return true;
    }

    private static string ContentTypeOf(string extension) => extension switch
    {
        ".pdf" => "application/pdf",
        ".xml" => "application/xml",
        ".zip" => "application/zip",
        _ => "application/octet-stream",
    };
}
