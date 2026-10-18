using Microsoft.Extensions.Logging.Abstractions;
using WebProveedores.Application.Documents;
using WebProveedores.Application.Ports.Outbound.Files;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Organization;
using WebProveedores.Infrastructure.Files;

namespace WebProveedores.Tests;

/// <summary>Archivos del documento para el correo de observación (flujo 05).</summary>
public sealed class DocumentFilesTests
{
    [Fact]
    public async Task Attachments_over_the_email_limit_are_not_read_nor_sent()
    {
        var storage = new UnreadableStorage();
        var files = new DocumentFiles(storage, new PdfSharpMerger(), TimeProvider.System, NullLogger<DocumentFiles>.Instance);
        var document = SupplierDocument.Register(
            new DocumentData("F001-1", DocumentEntryType.Special, "Boleto aéreo", "20100003199", "Proveedor", null, Currency.PEN, 100m, null, 100m, "Boleto", new DateOnly(2026, 10, 1), "Validado"),
            new Company { Code = "1001", Name = "Naviera" }, Guid.CreateVersion7(), "Usuario (interno)", DateTime.UtcNow, "Validado en SAP");
        document.AddAttachment(AttachmentKind.Pdf, "boleto.pdf", "clave", "application/pdf", DocumentFiles.MaxEmailAttachmentBytes + 1, DateTime.UtcNow);

        Assert.Null(await files.ForEmailAsync(document, CancellationToken.None));
        Assert.False(storage.Read);
    }

    [Fact]
    public async Task Unreadable_files_send_the_email_without_attachments()
    {
        var storage = new UnreadableStorage();
        var files = new DocumentFiles(storage, new PdfSharpMerger(), TimeProvider.System, NullLogger<DocumentFiles>.Instance);
        var document = SupplierDocument.Register(
            new DocumentData("F001-2", DocumentEntryType.Special, "Boleto aéreo", "20100003199", "Proveedor", null, Currency.PEN, 100m, null, 100m, "Boleto", new DateOnly(2026, 10, 1), "Validado"),
            new Company { Code = "1001", Name = "Naviera" }, Guid.CreateVersion7(), "Usuario (interno)", DateTime.UtcNow, "Validado en SAP");
        document.AddAttachment(AttachmentKind.Pdf, "boleto.pdf", "clave", "application/pdf", 1024, DateTime.UtcNow);

        Assert.Null(await files.ForEmailAsync(document, CancellationToken.None));
        Assert.True(storage.Read);
    }

    private sealed class UnreadableStorage : IFileStorage
    {
        public bool Read { get; private set; }

        public Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken) => Task.FromResult("clave");

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
        {
            Read = true;
            throw new FileNotFoundException(storageKey);
        }

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
