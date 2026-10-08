namespace WebProveedores.Application.Ports.Outbound.Sap.Models;

/// <summary>Factura del proveedor con su estado en SAP (Recepcionado, Pagado, Documento Anulado…).</summary>
public sealed record SapInvoice(
    SapDocumentNumber Document,
    string ProviderRuc,
    string? CompanyRuc,
    decimal Amount,
    string Currency,
    DateOnly? IssuedAt,
    bool HasDetraction,
    bool HasRetention,
    string Status);
