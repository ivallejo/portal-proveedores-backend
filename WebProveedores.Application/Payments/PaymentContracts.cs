namespace WebProveedores.Application.Payments;

/// <summary>Filtros comunes. El proveedor siempre consulta su propio RUC; CxP y el administrador indican el RUC.</summary>
public sealed record PaymentSearchRequest(string? Ruc, string? CompanyCode, DateOnly From, DateOnly To);

public sealed record InvoiceSearchRequest(string? Ruc, string? CompanyCode, string? Number, DateOnly From, DateOnly To);

public sealed record PaymentOrderResponse(
    string Number,
    DateOnly? PaidAt,
    string CompanyCode,
    string CompanyName,
    string? CompanyRuc,
    string ProviderRuc,
    string ProviderName,
    string Currency,
    decimal Total,
    string PaymentMethod,
    string? Bank,
    string? Account,
    string PaymentDocument,
    IReadOnlyList<PaidDocumentResponse> Documents);

public sealed record PaidDocumentResponse(
    string Number,
    string Type,
    DateOnly? IssuedAt,
    decimal Amount,
    decimal Retention,
    decimal Detraction,
    decimal Paid,
    string? RetentionDocument,
    string? DetractionCertificate);

public sealed record InvoiceStatusResponse(
    string Number,
    string Type,
    string ProviderRuc,
    string? CompanyCode,
    string? CompanyName,
    string? CompanyRuc,
    decimal Amount,
    string Currency,
    DateOnly? IssuedAt,
    bool HasDetraction,
    bool HasRetention,
    /// <summary>Estado tal como lo informa SAP (Recepcionado, Pagado, Documento Anulado…).</summary>
    string Status);

/// <summary>Órdenes de pago y estado de facturas del proveedor, consultados en SAP.</summary>
public interface IPaymentQueryService
{
    Task<IReadOnlyList<PaymentOrderResponse>> SearchPaymentOrdersAsync(Guid userId, PaymentSearchRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<InvoiceStatusResponse>> SearchInvoicesAsync(Guid userId, InvoiceSearchRequest request, CancellationToken cancellationToken);
}
