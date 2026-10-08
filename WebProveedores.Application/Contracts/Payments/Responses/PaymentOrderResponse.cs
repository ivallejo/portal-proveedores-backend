namespace WebProveedores.Application.Contracts.Payments.Responses;

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
