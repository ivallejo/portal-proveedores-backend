using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Application.Contracts.Access.Requests;

public sealed class MenuRequest
{
    [Required, MaxLength(80)] public string Name { get; init; } = string.Empty;
    [MaxLength(200)] public string? Route { get; init; }
    [Required, MaxLength(40)] public string Icon { get; init; } = string.Empty;
    [Range(1, 99)] public int Order { get; init; } = 1;
    public Guid? ParentId { get; init; }
}
