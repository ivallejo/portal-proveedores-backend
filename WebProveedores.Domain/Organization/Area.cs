using System.Globalization;
using System.Text;
using WebProveedores.Domain.Identity;

namespace WebProveedores.Domain.Organization;

/// <summary>Unidad interna de una sociedad. Los usuarios internos (aprobadores, contabilidad) pertenecen a un área.</summary>
public sealed class Area
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    /// <summary>Clave derivada del nombre, única dentro de la sociedad.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<AppUser> Users { get; set; } = [];

    /// <summary>«Recursos Humanos» → «RECURSOS_HUMANOS»: sin tildes, mayúsculas y guiones bajos.</summary>
    public static string CodeFor(string name)
    {
        var normalized = name.Trim().Normalize(NormalizationForm.FormD)
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark);
        var code = string.Concat(normalized).ToUpperInvariant();
        return string.Concat(code.Select(character => char.IsLetterOrDigit(character) ? character : '_')).Trim('_');
    }
}
