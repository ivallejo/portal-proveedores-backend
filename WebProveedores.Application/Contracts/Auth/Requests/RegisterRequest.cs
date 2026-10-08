using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Application.Contracts.Auth.Requests;

public sealed class RegisterRequest
{
    [Required, MaxLength(20)] public string Ruc { get; init; } = string.Empty;
    [Required, MaxLength(200)] public string CompanyName { get; init; } = string.Empty;
    [Required, EmailAddress] public string Email { get; init; } = string.Empty;
    [Required, MinLength(8)] public string Password { get; init; } = string.Empty;
}
