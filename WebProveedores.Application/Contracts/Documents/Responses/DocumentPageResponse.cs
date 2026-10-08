using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Contracts.Documents.Responses;

public sealed record DocumentPageResponse(
    IReadOnlyList<DocumentSummaryResponse> Items,
    int Total,
    int Page,
    int PageSize,
    IReadOnlyDictionary<DocumentStatus, int> CountsByStatus);
