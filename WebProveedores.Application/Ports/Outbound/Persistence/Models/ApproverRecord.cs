namespace WebProveedores.Application.Ports.Outbound.Persistence.Models;

/// <summary>Usuario activo con rol de aprobador y área asignada.</summary>
public sealed record ApproverRecord(Guid UserId, string Name, string Email, Guid AreaId, string AreaName, string AreaCompanyCode, IReadOnlyList<string> CompanyCodes);
