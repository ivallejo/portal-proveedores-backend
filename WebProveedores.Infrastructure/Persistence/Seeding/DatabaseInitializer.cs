using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebProveedores.Domain.Access;

namespace WebProveedores.Infrastructure.Persistence.Seeding;

public sealed class DatabaseInitializer(
    AppDbContext db,
    ReferenceDataSeeder referenceData,
    IConfiguration configuration,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (configuration.GetValue("Database:ApplyMigrationsOnStartup", false))
            await db.Database.MigrateAsync(cancellationToken);

        await referenceData.SeedAsync(cancellationToken);
        await EnsureAdministratorAsync(cancellationToken);
    }

    /// <summary>
    /// Los administradores se crean solo con el seed. Sin ninguno activo nadie puede gestionar el portal,
    /// así que fuera de desarrollo la aplicación no arranca (<c>Seed:RequireAdministrator</c>, por defecto true).
    /// </summary>
    private async Task EnsureAdministratorAsync(CancellationToken cancellationToken)
    {
        var hasAdministrator = await db.Users.AnyAsync(
            user => user.IsActive && user.UserRoles.Any(userRole => userRole.Role.Code == SecurityCatalog.AdministratorRole),
            cancellationToken);
        if (hasAdministrator) return;

        const string message = "No hay ningún administrador activo. Agrega un usuario con rol ADMINISTRATOR al archivo de seed (Seed:FilePath) y reinicia la API.";
        if (configuration.GetValue("Seed:RequireAdministrator", true))
            throw new InvalidOperationException(message);
        logger.LogWarning(message);
    }
}
