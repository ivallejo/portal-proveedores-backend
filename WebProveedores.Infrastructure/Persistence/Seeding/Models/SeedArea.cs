namespace WebProveedores.Infrastructure.Persistence.Seeding.Models;

/// <summary>Área del archivo de seed: nombre, código de su sociedad y descripción opcional.</summary>
internal sealed class SeedArea
{
    public string Name { get; init; } = string.Empty;
    /// <summary>Códigos de las sociedades en las que existe el área.</summary>
    public List<string>? Companies { get; init; }
    /// <summary>Una sola sociedad (seeds anteriores); se suma a <see cref="Companies"/>.</summary>
    public string? Company { get; init; }
    public string? Description { get; init; }
}
