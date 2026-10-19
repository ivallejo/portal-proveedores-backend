using Microsoft.Extensions.Logging;
using WebProveedores.Infrastructure.Files;

namespace WebProveedores.Infrastructure.Files.Migration;

/// <summary>
/// Copia los adjuntos guardados en disco (<c>Storage:DocumentsPath</c>) al contenedor de Blob Storage con las mismas
/// claves, así los registros de <c>DocumentAttachments</c> siguen apuntando bien. No duplica los que ya existen.
/// Se ejecuta con <c>dotnet run --project WebProveedores.Api -- migrate-storage</c>.
/// </summary>
public sealed class StorageMigration(StorageSettings settings, ILogger<StorageMigration> logger)
{
    public async Task<(int Copied, int Skipped)> RunAsync(CancellationToken cancellationToken)
    {
        if (settings.Provider != StorageProvider.AzureBlob)
            throw new InvalidOperationException("Configura Storage:Provider=AzureBlob y Storage:ConnectionString antes de migrar.");
        var local = new LocalFileStorage(settings);
        var blob = new AzureBlobFileStorage(settings);
        await blob.EnsureContainerAsync(cancellationToken);
        int copied = 0, skipped = 0;
        foreach (var key in local.ListKeys())
        {
            await using var content = await local.OpenReadAsync(key, cancellationToken);
            if (await blob.UploadExistingAsync(key, content, cancellationToken)) copied++;
            else skipped++;
        }
        logger.LogInformation("Migración de adjuntos a {Storage}: {Copied} copiados, {Skipped} ya existían.", settings.Describe(), copied, skipped);
        return (copied, skipped);
    }
}
