namespace WebProveedores.Application.Ports.Outbound.Persistence;

/// <summary>Datos que no pueden repetirse entre usuarios: DNI, RUC, correo y nombre de usuario.</summary>
public interface IUserUniquenessChecker
{
    Task<bool> DniExistsAsync(string dni, CancellationToken cancellationToken);
    Task<bool> RucExistsAsync(string ruc, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken);
    /// <summary>El correo pertenece a un usuario distinto de <paramref name="exceptUserId"/>.</summary>
    Task<bool> EmailUsedByOtherAsync(string email, Guid exceptUserId, CancellationToken cancellationToken);
}
