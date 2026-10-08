namespace WebProveedores.Application.Contracts.Auth.Commands;

public sealed record LoginCommand
{
    public string Identifier { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
