namespace WebProveedores.Application.Contracts.Auth.Commands;

public sealed record ChangePasswordCommand
{
    /// <summary>
    /// Obligatoria en el cambio voluntario. En el cambio forzado de la contraseña temporal no se pide:
    /// la sesión se abrió con ella y solo permite cambiar la contraseña.
    /// </summary>
    public string? CurrentPassword { get; init; }
    public string NewPassword { get; init; } = string.Empty;
}
