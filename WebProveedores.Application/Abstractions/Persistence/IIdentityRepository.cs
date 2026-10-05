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
    Task<IReadOnlyList<AppUser>> ListUsersAsync(CancellationToken cancellationToken);
    Task<bool> UserExistsByRucAsync(string ruc, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken);
    Task<Role?> FindRoleAsync(string value, CancellationToken cancellationToken);
    Task<Role?> FindRoleByCodeAsync(string code, CancellationToken cancellationToken);
    void AddUser(AppUser user);
    void AddPasswordToken(PasswordResetToken token);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
