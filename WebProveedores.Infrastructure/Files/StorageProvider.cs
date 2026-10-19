namespace WebProveedores.Infrastructure.Files;

/// <summary>Dónde se guardan los adjuntos de los documentos (<c>Storage:Provider</c>).</summary>
public enum StorageProvider
{
    /// <summary>Disco del servidor (<c>Storage:DocumentsPath</c>).</summary>
    Local,

    /// <summary>Azure Blob Storage (Azurite en desarrollo).</summary>
    AzureBlob,
}
