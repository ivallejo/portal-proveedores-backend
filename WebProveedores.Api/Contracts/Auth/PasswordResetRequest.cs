using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Api.Contracts.Auth;

public sealed class PasswordResetRequest
{
    [Required, RegularExpression(@"^\d{11}$")] public string Ruc { get; init; } = string.Empty;
}
