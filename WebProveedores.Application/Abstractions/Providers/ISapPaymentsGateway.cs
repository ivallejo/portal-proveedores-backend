namespace WebProveedores.Application.Abstractions.Providers;

/// <summary>Consultas de pagos y facturas del proveedor en SAP (zconsopago y zconsfactu).</summary>
public interface ISapPaymentsGateway
{
    Task<IReadOnlyList<SapPaymentOrder>> FindPaymentOrdersAsync(string providerRuc, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<IReadOnlyList<SapInvoice>> FindInvoicesAsync(string providerRuc, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

/// <summary>Comprobante de SAP: tipo SUNAT (01 factura, 07 nota de crédito, 08 nota de débito…) y serie-número.</summary>
public sealed record SapDocumentNumber(string TypeCode, string Number);

/// <summary>Orden de pago ya ejecutada.</summary>
public sealed record SapPaymentOrder(
    string Number,
    DateOnly? PaidAt,
    string CompanyCode,
    string? CompanyRuc,
    string? CompanyName,
    string ProviderRuc,
    string ProviderName,
    string Currency,
    decimal Total,
    /// <summary>Código SAP del medio de pago (T transferencia, C cheque…).</summary>
    string PaymentMethodCode,
    string? Bank,
    string? Account,
    string PaymentDocument,
    IReadOnlyList<SapPaidDocument> Documents);

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
