namespace WebProveedores.Domain.Documents;

/// <summary>Datos del comprobante tal como llegan del XML o del formulario de documento especial.</summary>
public sealed record DocumentData(
    string Number,
    DocumentEntryType EntryType,
    string DocumentType,
    string ProviderRuc,
    string ProviderName,
    string? ProviderEmail,
    Currency Currency,
    decimal Subtotal,
    decimal? Igv,
    decimal Amount,
    string Concept,
    DateOnly IssuedAt,
    string Validation);
