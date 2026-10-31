namespace WebProveedores.Domain.Identity;

/// <summary>
/// Usuario de acceso del personal interno: texto libre de 4 a 50 caracteres (letras, números, «.», «_» y «-»), sin
/// espacios ni «@» y que no parezca un RUC (11 dígitos). El del proveedor es su RUC.
/// </summary>
public static class UsernamePolicy
{
    public const int MinLength = 4;
    public const int MaxLength = 50;

    /// <summary>Qué regla no cumple; vacío si es válido.</summary>
    public static string? Violation(string? username)
    {
        var value = username?.Trim() ?? string.Empty;
        if (value.Length is < MinLength or > MaxLength) return $"El usuario debe tener entre {MinLength} y {MaxLength} caracteres.";
        if (!value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'))
            return "El usuario solo puede tener letras, números, punto, guion y guion bajo (sin espacios, tildes ni @).";
        if (value.Length == 11 && value.All(char.IsAsciiDigit)) return "El usuario no puede ser un número de 11 dígitos: se confundiría con un RUC.";
        return null;
    }
}
