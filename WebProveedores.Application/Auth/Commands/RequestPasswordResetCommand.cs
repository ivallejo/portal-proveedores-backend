namespace WebProveedores.Application.Auth.Commands;

public sealed record RequestPasswordResetCommand
{
    /// <summary>RUC del proveedor o usuario del personal interno.</summary>
    public string Identifier { get; init; } = string.Empty;
}
