using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Api.Contracts.Admin;

public sealed class UserEmailInput
{
    /// <summary>Vacío para un correo nuevo.</summary>
    public Guid? Id { get; init; }
    [Required, MaxLength(320)] public string Email { get; init; } = string.Empty;
    /// <summary>work, billing o personal.</summary>
    public string Type { get; init; } = "work";
    public bool IsPrimary { get; init; }
}
