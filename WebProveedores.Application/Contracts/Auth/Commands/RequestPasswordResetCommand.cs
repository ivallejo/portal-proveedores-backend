namespace WebProveedores.Application.Contracts.Auth.Commands;

public sealed record RequestPasswordResetCommand
{
    public string Ruc { get; init; } = string.Empty;
}
