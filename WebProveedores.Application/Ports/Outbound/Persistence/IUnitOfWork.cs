namespace WebProveedores.Application.Ports.Outbound.Persistence;

/// <summary>Confirma en un solo paso los cambios hechos a través de los repositorios de identidad.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Ejecuta varios guardados como una sola transacción (todo o nada).</summary>
    Task InTransactionAsync(Func<Task> work, CancellationToken cancellationToken);
}
