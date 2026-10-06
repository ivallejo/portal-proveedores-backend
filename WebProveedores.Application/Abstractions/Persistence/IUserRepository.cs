using WebProveedores.Domain.Entities;

namespace WebProveedores.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task<AppUser?> FindForLoginAsync(string identifier, string normalizedEmail, CancellationToken cancellationToken);
    Task<AppUser?> FindByRucAsync(string ruc, CancellationToken cancellationToken);
    /// <summary>Lectura sin seguimiento: los cambios hechos sobre este usuario no se guardan.</summary>
    Task<AppUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>Usuario con seguimiento de cambios, para modificarlo y guardar.</summary>
    Task<AppUser?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>Búsqueda paginada por usuario, nombre, RUC o correo (sin seguimiento).</summary>
    Task<UserSearchResult> SearchAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<bool> RucExistsAsync(string ruc, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken);
    /// <summary>El correo pertenece a un usuario distinto de <paramref name="exceptUserId"/>.</summary>
    Task<bool> EmailUsedByOtherAsync(string email, Guid exceptUserId, CancellationToken cancellationToken);
    /// <summary>Hay otro administrador activo además de <paramref name="exceptUserId"/>.</summary>
    Task<bool> OtherActiveAdministratorExistsAsync(Guid exceptUserId, CancellationToken cancellationToken);
    /// <summary>Correo con un enlace de verificación vigente (con seguimiento, incluye al usuario).</summary>
    Task<UserEmail?> FindEmailByVerificationTokenAsync(string tokenHash, DateTime now, CancellationToken cancellationToken);
    void Add(AppUser user);
}

public sealed record UserSearchResult(IReadOnlyList<AppUser> Items, int Total);
