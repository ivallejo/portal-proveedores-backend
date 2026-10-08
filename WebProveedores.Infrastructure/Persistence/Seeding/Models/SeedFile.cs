namespace WebProveedores.Infrastructure.Persistence.Seeding.Models;

internal sealed class SeedFile
{
    public List<SeedCompany> Companies { get; init; } = [];
    public List<SeedArea> Areas { get; init; } = [];
    public List<SeedUser> Users { get; init; } = [];
}
