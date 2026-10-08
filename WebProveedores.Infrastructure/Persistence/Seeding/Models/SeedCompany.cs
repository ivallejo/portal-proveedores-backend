namespace WebProveedores.Infrastructure.Persistence.Seeding.Models;

internal sealed class SeedCompany
{
    public string Code { get; init; } = string.Empty;
    public string? Name { get; init; }
    public string? Ruc { get; init; }
    public string? BillingEmail { get; init; }
}
