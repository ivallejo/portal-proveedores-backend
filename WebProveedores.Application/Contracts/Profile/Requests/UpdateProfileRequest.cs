using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Application.Contracts.Profile.Requests;

/// <summary>Datos que la persona puede cambiar: razón social (proveedor) o nombres y apellidos (personal interno).</summary>
public sealed class UpdateProfileRequest
{
    [MaxLength(200)] public string? BusinessName { get; init; }
    [MaxLength(100)] public string? FirstName { get; init; }
    [MaxLength(100)] public string? LastName { get; init; }
}
