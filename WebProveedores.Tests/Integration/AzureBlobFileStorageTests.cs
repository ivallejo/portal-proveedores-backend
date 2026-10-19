using System.Text;
using Testcontainers.Azurite;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Infrastructure.Files;
using WebProveedores.Infrastructure.Files.Migration;

namespace WebProveedores.Tests.Integration;

/// <summary>Adjuntos en Blob Storage contra Azurite real (requiere Docker).</summary>
[Trait("Category", "Integration")]
public sealed class AzureBlobFileStorageTests : IAsyncLifetime
{
    private readonly AzuriteContainer azurite = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:latest").WithCommand("--skipApiVersionCheck").Build();
    private AzureBlobFileStorage storage = null!;
    private StorageSettings settings = null!;

    public async Task InitializeAsync()
    {
        await azurite.StartAsync();
        settings = new StorageSettings(StorageProvider.AzureBlob, azurite.GetConnectionString(), "documents", Path.Combine(Path.GetTempPath(), $"adjuntos-{Guid.NewGuid():N}"));
        storage = new AzureBlobFileStorage(settings);
        await storage.EnsureContainerAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await azurite.DisposeAsync();
        if (Directory.Exists(settings.DocumentsPath)) Directory.Delete(settings.DocumentsPath, recursive: true);
    }

    [Fact]
    public async Task Saves_reads_and_deletes_a_file()
    {
        var key = await storage.SaveAsync(new MemoryStream("%PDF-1.4 prueba"u8.ToArray()), ".pdf", CancellationToken.None);

        Assert.Matches(@"^\d{4}/\d{2}/[0-9a-f]{32}\.pdf$", key);
        await using (var read = await storage.OpenReadAsync(key, CancellationToken.None))
        using (var reader = new StreamReader(read, Encoding.UTF8))
            Assert.Equal("%PDF-1.4 prueba", await reader.ReadToEndAsync());

        await storage.DeleteAsync(key, CancellationToken.None);
        await Assert.ThrowsAsync<NotFoundException>(() => storage.OpenReadAsync(key, CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_invalid_keys_and_extensions()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => storage.OpenReadAsync("../secreto.pdf", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => storage.SaveAsync(new MemoryStream([1]), ".exe", CancellationToken.None));
    }

    [Fact]
    public async Task Migration_copies_disk_files_with_the_same_keys_once()
    {
        var local = Path.Combine(settings.DocumentsPath, "2026", "10");
        Directory.CreateDirectory(local);
        await File.WriteAllTextAsync(Path.Combine(local, "0001.xml"), "<Invoice/>");
        var migration = new StorageMigration(settings, Microsoft.Extensions.Logging.Abstractions.NullLogger<StorageMigration>.Instance);

        Assert.Equal((1, 0), await migration.RunAsync(CancellationToken.None));
        Assert.Equal((0, 1), await migration.RunAsync(CancellationToken.None));
        await using var read = await storage.OpenReadAsync("2026/10/0001.xml", CancellationToken.None);
        Assert.Equal("<Invoice/>", await new StreamReader(read).ReadToEndAsync());
    }
}
