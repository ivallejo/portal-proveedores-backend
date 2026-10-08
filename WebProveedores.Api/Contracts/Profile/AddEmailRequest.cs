using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Api.Contracts.Profile;

public sealed class AddEmailRequest
{
    [Required, MaxLength(320)] public string Email { get; init; } = string.Empty;
    /// <summary>work, billing o personal.</summary>
    [Required] public string Type { get; init; } = "work";
}
