using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Application.Contracts.Auth.Requests;

public sealed class LoginRequest
{
    [Required, MaxLength(320)] public string Identifier { get; init; } = string.Empty;
    [Required] public string Password { get; init; } = string.Empty;
}
