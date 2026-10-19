using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Application.Ports.Outbound.Files;

namespace WebProveedores.Infrastructure.Files;

/// <summary>
/// Guarda los adjuntos en disco con nombres generados (nunca el nombre del usuario),
/// agrupados por año y mes. Ruta configurable con <c>Storage:DocumentsPath</c> (con <c>Storage:Provider=Local</c>).
/// </summary>
public sealed class LocalFileStorage(StorageSettings settings) : IFileStorage
{
    private readonly string root = Path.GetFullPath(settings.DocumentsPath);

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        var key = StorageKeys.New(extension, DateTime.UtcNow);
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(file, cancellationToken);
        return key;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = Resolve(storageKey);
        if (!File.Exists(path)) throw new NotFoundException("El archivo no está disponible.");
        return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = Resolve(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    /// <summary>Claves de todos los archivos guardados (para migrarlos a Blob Storage).</summary>
    public IEnumerable<string> ListKeys() =>
        Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'))
            : [];

    private string Resolve(string storageKey)
    {
        var path = Path.GetFullPath(Path.Combine(root, storageKey));
        // Evita salir de la carpeta raíz con claves manipuladas.
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Clave de archivo no válida.");
        return path;
    }
}
