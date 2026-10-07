using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Ports.Outbound.Persistence;

namespace WebProveedores.Infrastructure.Persistence;

public sealed class EfUnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken) => await db.SaveChangesAsync(cancellationToken);

    public async Task InTransactionAsync(Func<Task> work, CancellationToken cancellationToken)
    {
        // El proveedor InMemory de las pruebas no maneja transacciones.
        if (!db.Database.IsRelational())
        {
            await work();
            return;
        }
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await work();
        await transaction.CommitAsync(cancellationToken);
    }
}
