using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WebProveedores.Domain.Identity;

/// <summary>
/// Política de contraseñas de la compañía (3.1), única para activación, recuperación, cambio y contraseñas temporales:
/// mínimo 12 caracteres, 3 de 4 tipos, sin el usuario ni sus nombres y sin palabras comunes. El historial y la vigencia
/// se aplican en <see cref="AppUser"/>.
/// </summary>
public static class PasswordPolicy
{
    public const int MinimumLength = 12;
    public const int RequiredCharacterKinds = 3;
    /// <summary>La nueva contraseña no puede repetir ninguna de las últimas 5 (la actual incluida).</summary>
    public const int HistorySize = 5;
    public const int ValidityDays = 90;

    public const string Description =
        "La contraseña debe tener mínimo 12 caracteres y combinar al menos 3 de estos tipos: mayúsculas, minúsculas, números y caracteres especiales. No puede contener tu usuario ni tus nombres, ni ser una palabra común.";
    public const string TooShort = "Debe tener al menos 12 caracteres.";
    public const string TooFewKinds = "Debe combinar al menos 3 de estos tipos: mayúsculas, minúsculas, números y caracteres especiales.";
    public const string ContainsPersonalData = "No puede contener tu usuario, tu documento ni tus nombres.";
    public const string TooCommon = "Es una contraseña o palabra demasiado común.";
    public const string Reused = "La nueva contraseña debe ser distinta de tus últimas 5 contraseñas.";

    // Palabras que no cuentan como nombre propio al partir la razón social o el nombre completo.
    private static readonly HashSet<string> IgnoredNameWords = ["del", "las", "los", "sac", "saa", "srl", "eirl", "sas", "the", "and", "por", "con"];

    /// <summary>Reglas incumplidas (vacía si la contraseña es válida). <paramref name="personalData"/>: usuario, documento y nombres.</summary>
    public static IReadOnlyList<string> Violations(string? password, IEnumerable<string?> personalData)
    {
        password ??= string.Empty;
        var violations = new List<string>();
        if (password.Length < MinimumLength) violations.Add(TooShort);
        if (CharacterKinds(password) < RequiredCharacterKinds) violations.Add(TooFewKinds);

        var normalized = Normalize(password);
        if (PersonalTerms(personalData).Any(normalized.Contains)) violations.Add(ContainsPersonalData);
        if (IsCommon(normalized)) violations.Add(TooCommon);
        return violations;
    }

    public static bool IsSatisfiedBy(string? password, IEnumerable<string?> personalData) => Violations(password, personalData).Count == 0;

    private static int CharacterKinds(string password) =>
        (password.Any(char.IsUpper) ? 1 : 0) + (password.Any(char.IsLower) ? 1 : 0)
        + (password.Any(char.IsDigit) ? 1 : 0) + (password.Any(character => !char.IsLetterOrDigit(character)) ? 1 : 0);

    /// <summary>Cada dato completo y cada palabra de 3 letras o más, en minúsculas y sin tildes.</summary>
    private static IEnumerable<string> PersonalTerms(IEnumerable<string?> personalData)
    {
        foreach (var value in personalData)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            var normalized = Normalize(value.Trim());
            if (normalized.Length >= 3 && !normalized.Contains(' ')) yield return normalized;
            foreach (var word in Regex.Split(normalized, @"[^\p{L}\p{Nd}]+"))
                if (word.Length >= 3 && !IgnoredNameWords.Contains(word)) yield return word;
        }
    }

    /// <summary>Común si la contraseña, o lo que queda al quitar números y símbolos de los extremos, está en la lista.</summary>
    private static bool IsCommon(string normalized)
    {
        var core = normalized.Trim(normalized.Where(character => !char.IsLetter(character)).Distinct().ToArray());
        return CommonPasswords.Contains(normalized) || (core.Length > 0 && CommonPasswords.Contains(core));
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark) builder.Append(char.ToLowerInvariant(character));
        return builder.ToString();
    }
}
