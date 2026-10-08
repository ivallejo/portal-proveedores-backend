namespace WebProveedores.Application.Ports.Outbound.Sap.Models;

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
