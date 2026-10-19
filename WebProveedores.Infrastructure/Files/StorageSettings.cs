using Microsoft.Extensions.Configuration;

namespace WebProveedores.Infrastructure.Files;

/// <summary>Configuración del almacenamiento de adjuntos, validada al arrancar.</summary>
public sealed record StorageSettings(StorageProvider Provider, string? ConnectionString, string Container, string DocumentsPath)
{
    public static StorageSettings From(IConfiguration configuration)
    {
        var providerText = configuration["Storage:Provider"];
        var provider = string.IsNullOrWhiteSpace(providerText)
            ? StorageProvider.Local
            : Enum.TryParse<StorageProvider>(providerText, ignoreCase: true, out var parsed)
                ? parsed
                : throw new InvalidOperationException($"Storage:Provider «{providerText}» no es válido (Local o AzureBlob).");
        var connectionString = configuration["Storage:ConnectionString"];
        if (provider == StorageProvider.AzureBlob && string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Storage:Provider=AzureBlob necesita Storage:ConnectionString.");
        var container = configuration["Storage:Container"];
        return new StorageSettings(
            provider,
            connectionString,
            string.IsNullOrWhiteSpace(container) ? "documents" : container.Trim().ToLowerInvariant(),
            configuration["Storage:DocumentsPath"] ?? Path.Combine("App_Data", "documents"));
    }

    /// <summary>Texto para el log de arranque, sin la clave de la cuenta.</summary>
    public string Describe() => Provider switch
    {
        StorageProvider.AzureBlob when ConnectionString!.Contains("UseDevelopmentStorage=true", StringComparison.OrdinalIgnoreCase)
            => $"Azure Blob · Azurite (contenedor «{Container}»)",
        StorageProvider.AzureBlob => $"Azure Blob · cuenta {AccountName()} (contenedor «{Container}»)",
        _ => $"disco local ({Path.GetFullPath(DocumentsPath)})",
    };

    private string AccountName() =>
        ConnectionString!.Split(';').Select(part => part.Split('=', 2))
            .FirstOrDefault(pair => pair.Length == 2 && pair[0].Trim().Equals("AccountName", StringComparison.OrdinalIgnoreCase))?[1] ?? "(sin nombre)";
}
