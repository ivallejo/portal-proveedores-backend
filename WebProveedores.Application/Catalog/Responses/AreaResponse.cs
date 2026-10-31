namespace WebProveedores.Application.Catalog.Responses;

/// <param name="CompanyCode">Sociedad a la que pertenece el área.</param>
/// <summary>Área con aprobadores; <paramref name="CompanyCodes"/>: sociedades en las que existe.</summary>
public sealed record AreaResponse(Guid Id, string Name, IReadOnlyList<string> CompanyCodes, IReadOnlyList<ApproverResponse> Approvers);
