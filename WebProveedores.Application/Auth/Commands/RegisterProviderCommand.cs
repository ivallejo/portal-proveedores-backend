namespace WebProveedores.Application.Auth.Commands;

public sealed record RegisterProviderCommand
{
    public string Ruc { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
