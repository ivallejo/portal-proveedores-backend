namespace WebProveedores.Application.Contracts.Access.Commands;

public sealed record SaveMenuCommand
{
    public string Name { get; init; } = string.Empty;
    public string? Route { get; init; }
    public string Icon { get; init; } = string.Empty;
    public int Order { get; init; } = 1;
    public Guid? ParentId { get; init; }
}
