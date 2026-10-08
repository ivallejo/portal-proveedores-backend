using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Application.Contracts.Access.Requests;

public sealed class RoleRequest
{
    [Required, MaxLength(120)] public string Name { get; init; } = string.Empty;
    [MaxLength(300)] public string? Description { get; init; }
    public IReadOnlyList<Guid> MenuIds { get; init; } = [];
}
