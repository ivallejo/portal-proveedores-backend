namespace WebProveedores.Infrastructure.Files;

/// <summary>Claves de los adjuntos: generadas (nunca el nombre del usuario) y agrupadas por año y mes.</summary>
public static class StorageKeys
{
    private static readonly string[] AllowedExtensions = [".pdf", ".xml", ".zip"];

    public static string New(string extension, DateTime now)
    {
        if (!AllowedExtensions.Contains(extension)) throw new ArgumentException("Tipo de archivo no permitido.");
        return $"{now:yyyy}/{now:MM}/{Guid.CreateVersion7():N}{extension}";
    }

    /// <summary>Solo claves con el formato generado: evita rutas manipuladas («..», «/» al inicio).</summary>
    public static bool IsValid(string key) =>
        !string.IsNullOrWhiteSpace(key) && !key.StartsWith('/') && !key.Contains("..", StringComparison.Ordinal) && !key.Contains('\\');
}
