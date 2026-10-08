namespace WebProveedores.Application.Contracts.Payments.Responses;

public sealed record PaidDocumentResponse(
    string Number,
    string Type,
    DateOnly? IssuedAt,
    decimal Amount,
    decimal Retention,
    decimal Detraction,
    decimal Paid,
    string? RetentionDocument,
    string? DetractionCertificate,
    string? DetractionRate);
