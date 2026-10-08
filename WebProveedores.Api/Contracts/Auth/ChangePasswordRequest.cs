using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Api.Contracts.Auth;

public sealed class ChangePasswordRequest
{
    /// <summary>
    /// Obligatoria en el cambio voluntario. En el cambio forzado de la contraseña temporal no se pide:
    /// la sesión se abrió con ella y solo permite cambiar la contraseña.
    /// </summary>
    public string? CurrentPassword { get; init; }
    [Required, MinLength(8), MaxLength(128)] public string NewPassword { get; init; } = string.Empty;
}
