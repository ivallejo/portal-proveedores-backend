namespace WebProveedores.Application.Contracts.Access.Commands;

public sealed record SaveRoleCommand
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public IReadOnlyList<Guid> MenuIds { get; init; } = [];
}
