namespace WebProveedores.Application.Organization.Commands;

public sealed record SaveAreaCommand
{
    public Guid CompanyId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}
