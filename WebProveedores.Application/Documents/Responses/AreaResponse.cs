namespace WebProveedores.Application.Documents.Responses;

/// <param name="CompanyCode">Sociedad a la que pertenece el área.</param>
public sealed record AreaResponse(Guid Id, string Name, string CompanyCode, IReadOnlyList<ApproverResponse> Approvers);
