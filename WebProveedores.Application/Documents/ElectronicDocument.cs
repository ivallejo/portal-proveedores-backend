using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Documents;

/// <summary>Datos de un comprobante electrónico UBL 2.1 (factura, boleta, nota de crédito o débito).</summary>
internal sealed record ElectronicDocument(
    string Number,
    string Series,
    string DocumentType,
    string IssuerRuc,
    string IssuerName,
    string ReceiverRuc,
    string ReceiverName,
    DateOnly IssuedAt,
    DateOnly? DueAt,
    Currency Currency,
    IReadOnlyList<ElectronicDocumentLine> Lines,
    decimal Subtotal,
    decimal? Igv,
    decimal Total)
{
    /// <summary>Las series que comienzan con «E» se emiten desde SUNAT y no requieren CDR.</summary>
    public bool RequiresCdr => !Series.StartsWith('E');
}
