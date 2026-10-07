namespace WebProveedores.Domain.Documents;

/// <summary>Aprobador de área asignado al documento.</summary>
public sealed record ApproverAssignment(Guid AreaId, string AreaName, Guid ApproverId, string Name, string Email);
