namespace WebProveedores.Application.Profile.Commands;

public sealed record AddEmailCommand
{
    public string Email { get; init; } = string.Empty;
    /// <summary>work, billing o personal.</summary>
    public string Type { get; init; } = "work";
}
