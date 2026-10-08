namespace WebProveedores.Infrastructure.Persistence.Seeding.Models;

internal sealed class SeedUser
{
    public string Username { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string? Area { get; init; }
    public string? Ruc { get; init; }
    /// <summary>DNI del personal interno (también sirve para ingresar).</summary>
    public string? Dni { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    /// <summary>Códigos de sociedad; si se omite, el usuario trabaja con todas las sociedades activas.</summary>
    public List<string>? Companies { get; init; }
    public string? TemporaryPassword { get; init; }
}
