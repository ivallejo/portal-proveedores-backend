namespace WebProveedores.Application.Payments.Responses;

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
