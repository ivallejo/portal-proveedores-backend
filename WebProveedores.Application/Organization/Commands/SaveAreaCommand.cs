namespace WebProveedores.Application.Organization.Commands;

public sealed record SaveAreaCommand
{
    /// <summary>Sociedades en las que existe el área (al menos una).</summary>
    public IReadOnlyList<Guid> CompanyIds { get; init; } = [];
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}
