using WebProveedores.Application.Documents.Commands;
using WebProveedores.Application.Documents.Responses;
using WebProveedores.Application.Ports.Outbound.Sap;

namespace WebProveedores.Application.Documents;

internal sealed class DocumentCatalogService(DocumentAccess access, ISapDocumentGateway sap) : IDocumentCatalogService
{
    public async Task<OrderValidationResponse?> ValidateOrderAsync(Guid userId, ValidateOrderCommand request, CancellationToken cancellationToken)
    {
        var actor = await access.LoadActorAsync(userId, cancellationToken);
        var company = await access.RequireCompanyAsync(actor, request.CompanyCode, cancellationToken);
        var order = await sap.ValidateOrderAsync(company.Code, request.OrderType, request.Number.Trim().ToUpperInvariant(), cancellationToken);
        return order is null ? null : new OrderValidationResponse(order.Number, order.Type, order.Description, order.Balance);
    }
}
