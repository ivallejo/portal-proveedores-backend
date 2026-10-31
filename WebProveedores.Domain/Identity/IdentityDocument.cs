using WebProveedores.Domain.Common;

namespace WebProveedores.Domain.Identity;

/// <summary>Reglas del número de documento: DNI de 8 dígitos; carné de extranjería y pasaporte, letras y números hasta 12.</summary>
public static class IdentityDocument
{
    public const int MaxLength = 12;

    /// <summary>Número sin espacios y en mayúsculas; lanza <see cref="DomainRuleException"/> si no es válido para su tipo.</summary>
    public static string Normalize(IdentityDocumentType type, string? number)
    {
        var value = (number ?? string.Empty).Trim().ToUpperInvariant();
        var valid = type == IdentityDocumentType.Dni
            ? value.Length == 8 && value.All(char.IsAsciiDigit)
            : value.Length is > 0 and <= MaxLength && value.All(char.IsAsciiLetterOrDigit);
        if (!valid) throw new DomainRuleException(type switch
        {
            IdentityDocumentType.Dni => "El DNI debe tener 8 dígitos.",
            IdentityDocumentType.ForeignerCard => $"El carné de extranjería debe tener solo letras y números, hasta {MaxLength}.",
            _ => $"El pasaporte debe tener solo letras y números, hasta {MaxLength}.",
        });
        return value;
    }
}
