using WebProveedores.Domain.Identity;

namespace WebProveedores.Application.Ports.Outbound.Persistence;

/// <summary>Datos que no pueden repetirse entre usuarios: documento, RUC, correo y nombre de usuario.</summary>
public interface IUserUniquenessChecker
{
    Task<bool> DocumentExistsAsync(IdentityDocumentType type, string number, CancellationToken cancellationToken);
    Task<bool> RucExistsAsync(string ruc, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken);
    /// <summary>El usuario (sin distinguir mayúsculas) pertenece a una cuenta distinta de <paramref name="exceptUserId"/>.</summary>
    Task<bool> UsernameUsedByOtherAsync(string username, Guid? exceptUserId, CancellationToken cancellationToken);
    /// <summary>El correo pertenece a un usuario distinto de <paramref name="exceptUserId"/>.</summary>
    Task<bool> EmailUsedByOtherAsync(string email, Guid exceptUserId, CancellationToken cancellationToken);
}
