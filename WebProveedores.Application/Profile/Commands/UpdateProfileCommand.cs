namespace WebProveedores.Application.Profile.Commands;

/// <summary>Datos que la persona puede cambiar: razón social (proveedor) o nombres y apellidos (personal interno).</summary>
public sealed record UpdateProfileCommand
{
    public string? BusinessName { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
}
