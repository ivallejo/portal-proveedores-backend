namespace WebProveedores.Application.Admin.Commands;

/// <summary>
/// Alta y edición de un usuario (un solo rol). Proveedor: RUC (también es su usuario) y razón social. Personal interno:
/// documento (solo al crear), usuario libre (editable), nombres, apellidos y área.
/// </summary>
public sealed record SaveUserCommand
{
    public string Role { get; init; } = string.Empty;
    /// <summary>DNI, CE o PASAPORTE (personal interno, solo al crear).</summary>
    public string? DocumentType { get; init; }
    /// <summary>RUC del proveedor o número del documento del interno (solo al crear).</summary>
    public string? Document { get; init; }
    /// <summary>Usuario de acceso del personal interno; el del proveedor es su RUC.</summary>
    public string? Username { get; init; }
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
