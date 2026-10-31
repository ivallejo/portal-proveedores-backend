using System.Globalization;
using System.Text;
using WebProveedores.Domain.Identity;

namespace WebProveedores.Domain.Organization;

/// <summary>
/// Unidad interna presente en una o más sociedades. Los usuarios internos (aprobadores, contabilidad) pertenecen a un área.
/// </summary>
public sealed class Area
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    /// <summary>Clave derivada del nombre, única en todo el portal.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<AppUser> Users { get; set; } = [];
    public ICollection<AreaCompany> Companies { get; set; } = [];

    public IEnumerable<string> CompanyCodes => Companies.Select(item => item.Company.Code).Order();

    public bool HasCompany(Guid companyId) => Companies.Any(item => item.CompanyId == companyId);

    /// <summary>Deja exactamente estas sociedades (quita las demás y agrega las que falten).</summary>
    public void SetCompanies(IEnumerable<Company> companies)
    {
        var wanted = companies.DistinctBy(company => company.Id).ToList();
        foreach (var stale in Companies.Where(item => wanted.All(company => company.Id != item.CompanyId)).ToList())
            Companies.Remove(stale);
        foreach (var company in wanted.Where(company => Companies.All(item => item.CompanyId != company.Id)))
            Companies.Add(new AreaCompany { AreaId = Id, CompanyId = company.Id, Company = company });
    }

    /// <summary>«Recursos Humanos» → «RECURSOS_HUMANOS»: sin tildes, mayúsculas y guiones bajos.</summary>
    public static string CodeFor(string name)
    {
        var normalized = name.Trim().Normalize(NormalizationForm.FormD)
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark);
        var code = string.Concat(normalized).ToUpperInvariant();
        return string.Concat(code.Select(character => char.IsLetterOrDigit(character) ? character : '_')).Trim('_');
    }
}
