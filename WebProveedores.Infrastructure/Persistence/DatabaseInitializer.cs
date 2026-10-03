using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WebProveedores.Infrastructure.Auth;

namespace WebProveedores.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    AppDbContext db,
    AdminBootstrapper bootstrapper,
    IConfiguration configuration)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (configuration.GetValue("Database:ApplyMigrationsOnStartup", false))
            await db.Database.MigrateAsync(cancellationToken);

        await bootstrapper.EnsureAdminAsync(cancellationToken);
    }
}
