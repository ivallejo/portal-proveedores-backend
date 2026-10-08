using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Api.Contracts.Admin;

/// <summary>
/// Alta y edición de un usuario (un solo rol). Proveedor: RUC y razón social. Personal interno: DNI, nombres,
/// apellidos y área. El documento es el usuario de acceso: solo se indica al crear.
/// </summary>
public sealed record SaveUserRequest
{
    [Required, MaxLength(40)] public string Role { get; init; } = string.Empty;
    [MaxLength(11)] public string? Document { get; init; }
    [MaxLength(200)] public string? BusinessName { get; init; }
    [MaxLength(100)] public string? FirstName { get; init; }
    [MaxLength(100)] public string? LastName { get; init; }
    public Guid? AreaId { get; init; }
    public IReadOnlyList<string> CompanyCodes { get; init; } = [];
    public IReadOnlyList<UserEmailInput> Emails { get; init; } = [];
    /// <summary>active o inactive. «locked» deja el bloqueo como está (solo si ya estaba bloqueado).</summary>
    public string Status { get; init; } = "active";
    /// <summary>Solicitar cambio de contraseña en el próximo inicio (solo al editar).</summary>
    public bool MustChangePassword { get; init; }
}
