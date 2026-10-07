using WebProveedores.Application.Ports.Outbound.Persistence.Models;

namespace WebProveedores.Application.Ports.Outbound.Persistence;

/// <summary>Usuarios activos que pueden aprobar documentos, con su área y sociedades.</summary>
public interface IApproverDirectory
{
    Task<IReadOnlyList<ApproverRecord>> ListApproversAsync(CancellationToken cancellationToken);
    Task<ApproverRecord?> FindApproverAsync(Guid userId, CancellationToken cancellationToken);
}
