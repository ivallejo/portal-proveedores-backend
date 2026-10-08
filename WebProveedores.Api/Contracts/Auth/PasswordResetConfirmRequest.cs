using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Api.Contracts.Auth;

/// <summary>Enlace de activación o recuperación: identifica la cuenta con el RUC (proveedor) o el usuario (personal interno).</summary>
public sealed class PasswordResetConfirmRequest
{
    [RegularExpression(@"^\d{11}$")] public string? Ruc { get; init; }
    [MaxLength(80)] public string? User { get; init; }
    [Required] public string Token { get; init; } = string.Empty;
    [Required, MinLength(8), MaxLength(128)] public string NewPassword { get; init; } = string.Empty;
}
