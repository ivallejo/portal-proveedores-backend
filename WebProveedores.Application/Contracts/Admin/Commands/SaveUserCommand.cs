namespace WebProveedores.Application.Contracts.Admin.Commands;

/// <summary>
/// Alta y edición de un usuario (un solo rol). Proveedor: RUC y razón social. Personal interno: DNI, nombres,
/// apellidos y área. El documento es el usuario de acceso: solo se indica al crear.
/// </summary>
public sealed record SaveUserCommand
{
    public string Role { get; init; } = string.Empty;
    public string? Document { get; init; }
    public string? BusinessName { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public Guid? AreaId { get; init; }
    public IReadOnlyList<string> CompanyCodes { get; init; } = [];
    public IReadOnlyList<UserEmailData> Emails { get; init; } = [];
    /// <summary>active o inactive. «locked» deja el bloqueo como está (solo si ya estaba bloqueado).</summary>
    public string Status { get; init; } = "active";
    /// <summary>Solicitar cambio de contraseña en el próximo inicio (solo al editar).</summary>
    public bool MustChangePassword { get; init; }
}
