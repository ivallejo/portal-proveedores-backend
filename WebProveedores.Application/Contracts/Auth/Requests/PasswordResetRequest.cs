using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Application.Contracts.Auth.Requests;

public sealed class PasswordResetRequest
{
    [Required, RegularExpression(@"^\d{11}$")] public string Ruc { get; init; } = string.Empty;
}
