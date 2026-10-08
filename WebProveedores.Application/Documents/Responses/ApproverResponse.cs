namespace WebProveedores.Application.Documents.Responses;

/// <summary>Aprobador con las sociedades en las que puede aprobar (el frontend filtra por la sociedad del documento).</summary>
public sealed record ApproverResponse(Guid Id, string Name, string Email, IReadOnlyList<string> CompanyCodes);
