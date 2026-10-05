using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using WebProveedores.Infrastructure.Persistence;

namespace WebProveedores.Tests.Integration;

/// <summary>
/// SQL Server real en Docker (Testcontainers) con las migraciones aplicadas, compartido por las pruebas de integración.
/// Comprueba lo que EF InMemory no detecta: traducción de consultas a SQL, índices únicos y migraciones.
/// Requiere Docker; para omitirlas: <c>dotnet test --filter Category!=Integration</c>.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    // Misma imagen que docker-compose.yml.
    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer(container.GetConnectionString(), sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
        .Options);

    public Task DisposeAsync() => container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SQL Server";
}
