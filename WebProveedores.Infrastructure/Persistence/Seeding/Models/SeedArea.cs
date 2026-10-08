namespace WebProveedores.Infrastructure.Persistence.Seeding.Models;

/// <summary>Área del archivo de seed: nombre, código de su sociedad y descripción opcional.</summary>
internal sealed class SeedArea
{
    public string Name { get; init; } = string.Empty;
    public string? Company { get; init; }
    public string? Description { get; init; }
}
