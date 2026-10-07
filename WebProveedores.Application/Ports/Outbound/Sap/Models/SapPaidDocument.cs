namespace WebProveedores.Application.Ports.Outbound.Sap.Models;

public sealed record SapPaidDocument(
    SapDocumentNumber Document,
    DateOnly? IssuedAt,
    decimal Amount,
    decimal Retention,
    decimal Detraction,
    decimal Paid,
    string? RetentionDocument,
    string? DetractionCertificate,
    /// <summary>Tasa de la detracción tal como la informa SAP («12.0 %»).</summary>
    string? DetractionRate);
