using WebProveedores.Domain.Identity;

namespace WebProveedores.Application.Ports.Outbound.Persistence;

/// <summary>Usuarios con seguimiento de cambios, para modificarlos y guardar con <see cref="IUnitOfWork"/>.</summary>
public interface IUserRepository
{
    /// <summary>Cuenta por su usuario de acceso o, si es proveedor, por su RUC. El correo y el documento no sirven.</summary>
    Task<AppUser?> FindForLoginAsync(string username, CancellationToken cancellationToken);
    Task<AppUser?> FindByRucAsync(string ruc, CancellationToken cancellationToken);
    Task<AppUser?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>Correo con un enlace de verificación vigente (incluye al usuario).</summary>
    Task<UserEmail?> FindEmailByVerificationTokenAsync(string tokenHash, DateTime now, CancellationToken cancellationToken);
    void Add(AppUser user);
}
