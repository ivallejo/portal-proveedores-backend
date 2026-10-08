namespace WebProveedores.Application.Auth.Commands;

/// <summary>Enlace de activación o recuperación: identifica la cuenta con el RUC (proveedor) o el usuario (personal interno).</summary>
public sealed record ConfirmPasswordResetCommand
{
    public string? Ruc { get; init; }
    public string? User { get; init; }
    public string Token { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
}
