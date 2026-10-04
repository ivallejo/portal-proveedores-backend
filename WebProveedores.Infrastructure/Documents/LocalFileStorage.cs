using Microsoft.Extensions.Configuration;
using WebProveedores.Application.Abstractions.Documents;

namespace WebProveedores.Infrastructure.Documents;

/// <summary>
/// Guarda los adjuntos en disco con nombres generados (nunca el nombre del usuario),
/// agrupados por año y mes. Ruta configurable con <c>Storage:DocumentsPath</c>.
/// </summary>
public sealed class LocalFileStorage(IConfiguration configuration) : IFileStorage
{
    private readonly string root = Path.GetFullPath(configuration["Storage:DocumentsPath"] ?? Path.Combine("App_Data", "documents"));

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        if (extension is not (".pdf" or ".xml" or ".zip"))
            throw new ArgumentException("Tipo de archivo no permitido.");
        var now = DateTime.UtcNow;
        var key = $"{now:yyyy}/{now:MM}/{Guid.NewGuid():N}{extension}";
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(file, cancellationToken);
        return key;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = Resolve(storageKey);
        if (!File.Exists(path)) throw new KeyNotFoundException("El archivo no está disponible.");
        return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = Resolve(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string Resolve(string storageKey)
    {
        var path = Path.GetFullPath(Path.Combine(root, storageKey));
        // Evita salir de la carpeta raíz con claves manipuladas.
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Clave de archivo no válida.");
        return path;
    }
}
