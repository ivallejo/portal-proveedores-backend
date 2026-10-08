using WebProveedores.Domain.Organization;

namespace WebProveedores.Application.Ports.Outbound.Persistence;

/// <summary>Sociedades con seguimiento, para asignarlas a usuarios y documentos.</summary>
public interface ICompanyReader
{
    Task<Company?> FindByCodeAsync(string code, CancellationToken cancellationToken);
    /// <summary>Sociedades activas, ordenadas por código.</summary>
    Task<IReadOnlyList<Company>> ListActiveAsync(CancellationToken cancellationToken);
}
