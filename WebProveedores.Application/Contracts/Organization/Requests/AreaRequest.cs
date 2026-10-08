using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Application.Contracts.Organization.Requests;

public sealed class AreaRequest
{
    [Required] public Guid CompanyId { get; init; }
    [Required, MaxLength(120)] public string Name { get; init; } = string.Empty;
    [MaxLength(300)] public string? Description { get; init; }
}
