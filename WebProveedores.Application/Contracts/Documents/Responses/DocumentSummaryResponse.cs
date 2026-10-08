using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Contracts.Documents.Responses;

public sealed record DocumentSummaryResponse(
    Guid Id,
    string Number,
    DocumentEntryType EntryType,
    string DocumentType,
    string ProviderRuc,
    string ProviderName,
    Currency Currency,
    decimal Amount,
    DocumentStatus Status,
    bool IsPettyCash,
    DateOnly IssuedAt,
    DateTime RegisteredAtUtc,
    string? ApproverName,
    string? OrderNumber);
