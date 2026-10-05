using WebProveedores.Application.Abstractions.Documents;

namespace WebProveedores.Application.Documents;

internal sealed class DocumentCatalogService(IDocumentRepository documents, DocumentAccess access, ISapDocumentGateway sap) : IDocumentCatalogService
{
    /// <summary>Sociedades con las que trabaja el usuario (el administrador ve todas).</summary>
    public async Task<IReadOnlyList<CompanyResponse>> ListCompaniesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var actor = await access.LoadActorAsync(userId, cancellationToken);
        return (await documents.ListCompaniesAsync(cancellationToken)).Where(company => actor.HasCompany(company.Id)).Select(DocumentMapper.ToResponse).ToArray();
    }

    public async Task<IReadOnlyList<AreaResponse>> ListAreasAsync(CancellationToken cancellationToken)
    {
        var approvers = await documents.ListApproversAsync(cancellationToken);
        return approvers
            .GroupBy(approver => (approver.AreaId, approver.AreaName))
            .OrderBy(group => group.Key.AreaName)
            .Select(group => new AreaResponse(
                group.Key.AreaId,
                group.Key.AreaName,
                group.OrderBy(item => item.Name).Select(item => new ApproverResponse(item.UserId, item.Name, item.Email, item.CompanyCodes)).ToArray()))
            .ToArray();
    }

    public async Task<OrderValidationResponse?> ValidateOrderAsync(Guid userId, ValidateOrderRequest request, CancellationToken cancellationToken)
    {
        var actor = await access.LoadActorAsync(userId, cancellationToken);
        var company = await access.RequireCompanyAsync(actor, request.CompanyCode, cancellationToken);
        var order = await sap.ValidateOrderAsync(company.Code, request.OrderType, request.Number.Trim().ToUpperInvariant(), cancellationToken);
        return order is null ? null : new OrderValidationResponse(order.Number, order.Type, order.Description, order.Balance);
    }
}
