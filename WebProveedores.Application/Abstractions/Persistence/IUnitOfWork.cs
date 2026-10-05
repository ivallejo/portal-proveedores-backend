namespace WebProveedores.Application.Abstractions.Persistence;

/// <summary>Confirma en un solo paso los cambios hechos a través de los repositorios de identidad.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
