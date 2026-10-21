namespace WebProveedores.Domain.Identity;

/// <summary>Hash de una contraseña anterior del usuario, para no repetir las últimas (<see cref="PasswordPolicy.HistorySize"/>).</summary>
public sealed class PasswordHistoryEntry
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid UserId { get; init; }
    public string PasswordHash { get; init; } = string.Empty;
    /// <summary>Desde cuándo estuvo vigente esa contraseña.</summary>
    public DateTime SetAtUtc { get; init; }
}
