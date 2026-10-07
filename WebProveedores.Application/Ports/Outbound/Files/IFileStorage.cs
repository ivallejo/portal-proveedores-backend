namespace WebProveedores.Application.Ports.Outbound.Files;

/// <summary>Almacenamiento de los archivos adjuntos.</summary>
public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}
