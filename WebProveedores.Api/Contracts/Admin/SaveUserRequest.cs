using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Api.Contracts.Admin;

/// <summary>
/// Alta y edición de un usuario (un solo rol). Proveedor: RUC (también es su usuario) y razón social. Personal interno:
/// documento (solo al crear), usuario libre (editable), nombres, apellidos y área.
/// </summary>
public sealed record SaveUserRequest
{
    [Required, MaxLength(40)] public string Role { get; init; } = string.Empty;
    /// <summary>DNI, CE o PASAPORTE (personal interno, solo al crear).</summary>
    [MaxLength(20)] public string? DocumentType { get; init; }
    [MaxLength(12)] public string? Document { get; init; }
    [MaxLength(50)] public string? Username { get; init; }
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
