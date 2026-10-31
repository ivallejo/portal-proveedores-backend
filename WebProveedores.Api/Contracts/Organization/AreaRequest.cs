using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Api.Contracts.Organization;

public sealed class AreaRequest
{
    /// <summary>Sociedades en las que existe el área (al menos una).</summary>
    [Required] public IReadOnlyList<Guid> CompanyIds { get; init; } = [];
    [Required, MaxLength(120)] public string Name { get; init; } = string.Empty;
    [MaxLength(300)] public string? Description { get; init; }
}
