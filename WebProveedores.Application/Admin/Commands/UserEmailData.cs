namespace WebProveedores.Application.Admin.Commands;

public sealed record UserEmailData
{
    /// <summary>Vacío para un correo nuevo.</summary>
    public Guid? Id { get; init; }
    public string Email { get; init; } = string.Empty;
    /// <summary>work, billing o personal.</summary>
    public string Type { get; init; } = "work";
    public bool IsPrimary { get; init; }
}
