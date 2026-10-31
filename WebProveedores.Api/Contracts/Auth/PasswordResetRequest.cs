using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Api.Contracts.Auth;

/// <summary>RUC del proveedor o usuario del personal interno (como en el login). <c>Ruc</c> se acepta por compatibilidad.</summary>
public sealed class PasswordResetRequest : IValidatableObject
{
    [MaxLength(80)] public string? Identifier { get; init; }
    [MaxLength(11)] public string? Ruc { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Identifier) && string.IsNullOrWhiteSpace(Ruc))
            yield return new ValidationResult("Ingresa tu RUC o usuario.", [nameof(Identifier)]);
    }
}
