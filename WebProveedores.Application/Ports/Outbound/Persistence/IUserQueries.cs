using WebProveedores.Application.Ports.Outbound.Persistence.Models;
using WebProveedores.Domain.Identity;

namespace WebProveedores.Application.Ports.Outbound.Persistence;

/// <summary>Consultas de usuarios sin seguimiento: los cambios hechos sobre lo que devuelven no se guardan.</summary>
public interface IUserQueries
{
    Task<AppUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>Búsqueda paginada por usuario, nombre, RUC, DNI o correo, con filtros de rol y estado.</summary>
    Task<UserSearchResult> SearchAsync(UserSearchFilter filter, int page, int pageSize, CancellationToken cancellationToken);
    /// <summary>Total de usuarios y cuántos están activos (sin bloqueo vigente) en <paramref name="now"/>.</summary>
    Task<(int Total, int Active)> CountByStatusAsync(DateTime now, CancellationToken cancellationToken);
    /// <summary>Hay otro administrador activo además de <paramref name="exceptUserId"/>.</summary>
    Task<bool> OtherActiveAdministratorExistsAsync(Guid exceptUserId, CancellationToken cancellationToken);
}
