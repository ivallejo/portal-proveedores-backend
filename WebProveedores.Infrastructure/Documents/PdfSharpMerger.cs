using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using WebProveedores.Application.Ports.Outbound.Files;

namespace WebProveedores.Infrastructure.Documents;

/// <summary>Une PDF con PDFsharp (licencia MIT). Rechaza archivos cifrados o ilegibles.</summary>
public sealed class PdfSharpMerger : IPdfMerger
{
    public byte[] Merge(IReadOnlyList<byte[]> documents)
    {
        if (documents.Count == 0) throw new ArgumentException("No hay PDF para unir.", nameof(documents));

        using var output = new PdfDocument();
        foreach (var bytes in documents)
        {
            try
            {
                using var input = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
                foreach (var page in input.Pages) output.AddPage(page);
            }
            // PDFsharp lanza tipos distintos (incluso Exception genérica) con archivos dañados o cifrados.
            catch (Exception exception)
            {
                throw new InvalidDataException("Uno de los PDF no se pudo leer. Verifica que no esté dañado ni protegido con contraseña.", exception);
            }
        }

        using var result = new MemoryStream();
        output.Save(result, closeStream: false);
        return result.ToArray();
    }
}
