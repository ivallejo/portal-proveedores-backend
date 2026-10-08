using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Api.Contracts.Access;

public sealed class RoleRequest
{
    [Required, MaxLength(120)] public string Name { get; init; } = string.Empty;
    [MaxLength(300)] public string? Description { get; init; }
    public IReadOnlyList<Guid> MenuIds { get; init; } = [];
}
