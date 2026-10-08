using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Application.Ports.Outbound.Persistence;

namespace WebProveedores.Infrastructure.Persistence.Repositories;

public sealed class EfUnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateDocument(exception))
        {
            // Dos registros simultáneos del mismo comprobante: el índice único gana.
            throw new DocumentRejectedException("El documento ya fue registrado para ese RUC.");
        }
    }

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

    private static bool IsDuplicateDocument(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains("IX_Documents_ProviderRuc_Number", StringComparison.Ordinal) == true;
}
