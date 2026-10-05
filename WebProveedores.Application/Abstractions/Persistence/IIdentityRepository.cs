using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Entities;

namespace WebProveedores.Application.Abstractions.Persistence;

public interface IIdentityRepository
{
    Task<AppUser?> FindForLoginAsync(string identifier, string normalizedEmail, CancellationToken cancellationToken);
    Task<AppUser?> FindByRucAsync(string ruc, CancellationToken cancellationToken);
    /// <summary>Lectura sin seguimiento: los cambios hechos sobre este usuario no se guardan.</summary>
    Task<AppUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>Usuario con seguimiento de cambios, para modificarlo y guardar.</summary>
    Task<AppUser?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<PasswordResetToken?> FindValidTokenAsync(string ruc, string tokenHash, PasswordTokenPurpose purpose, CancellationToken cancellationToken);
    /// <summary>Búsqueda paginada por usuario, nombre, RUC o correo (sin seguimiento).</summary>
    Task<UserSearchResult> SearchUsersAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
    /// <summary>Sociedades activas, con seguimiento (para asignarlas a usuarios).</summary>
    Task<IReadOnlyList<Company>> ListActiveCompaniesAsync(CancellationToken cancellationToken);
    Task<bool> UserExistsByRucAsync(string ruc, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken);
    Task<IReadOnlyList<Role>> ListRolesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Area>> ListActiveAreasAsync(CancellationToken cancellationToken);
    Task<Area?> FindAreaAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>El correo pertenece a un usuario distinto de <paramref name="exceptUserId"/>.</summary>
    Task<bool> EmailUsedByOtherAsync(string email, Guid exceptUserId, CancellationToken cancellationToken);
    /// <summary>Hay otro administrador activo además de <paramref name="exceptUserId"/>.</summary>
    Task<bool> OtherActiveAdministratorExistsAsync(Guid exceptUserId, CancellationToken cancellationToken);
    Task<Role?> FindRoleByCodeAsync(string code, CancellationToken cancellationToken);
    void AddUser(AppUser user);
    void AddPasswordToken(PasswordResetToken token);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record UserSearchResult(IReadOnlyList<AppUser> Items, int Total);
