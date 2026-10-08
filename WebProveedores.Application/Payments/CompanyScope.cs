using WebProveedores.Application.Documents;
using WebProveedores.Domain.Organization;

namespace WebProveedores.Application.Payments;

/// <summary>Sociedades del catálogo y cuáles puede ver el usuario.</summary>
internal sealed class CompanyScope(DocumentActor actor, IReadOnlyList<Company> companies)
{
    public Company? ByCode(string code) => companies.FirstOrDefault(company => company.Code == code);

    public Company? ByRuc(string? ruc) => ruc is null ? null : companies.FirstOrDefault(company => company.Ruc == ruc);

    /// <summary>El administrador ve todas; los demás, solo las sociedades del catálogo que tienen asignadas.</summary>
    public bool Allows(string code) => actor.IsAdmin || (ByCode(code) is { } company && actor.HasCompany(company.Id));
}
