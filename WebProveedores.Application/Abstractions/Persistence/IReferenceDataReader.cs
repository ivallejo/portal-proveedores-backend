using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Entities;

namespace WebProveedores.Application.Abstractions.Persistence;

/// <summary>Catálogos de seguridad y organización: roles, áreas y sociedades (con seguimiento, para asignarlos).</summary>
public interface IReferenceDataReader
{
    Task<Role?> FindRoleAsync(string code, CancellationToken cancellationToken);
    Task<IReadOnlyList<Role>> ListRolesAsync(CancellationToken cancellationToken);
    Task<Area?> FindAreaAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Area>> ListActiveAreasAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Company>> ListActiveCompaniesAsync(CancellationToken cancellationToken);
}
