using WebProveedores.Domain.Access;

namespace WebProveedores.Application.UseCases.Documents;

/// <summary>
/// Quién actúa sobre los documentos. Lo que puede hacer sale de sus permisos (opciones de menú de su rol):
/// Registrar documentos, Documentos (aprobar) y Contabilización. El administrador ve todo y en todas las sociedades.
/// </summary>
internal sealed record DocumentActor(
    Guid Id,
    string Name,
    string Email,
    string? Ruc,
    Guid? AreaId,
    string? AreaName,
    IReadOnlySet<string> Roles,
    IReadOnlySet<string> Permissions,
    IReadOnlySet<Guid> CompanyIds)
{
    public bool IsAdmin => Roles.Contains(SecurityCatalog.AdministratorRole);
    /// <summary>El administrador trabaja con todas las sociedades.</summary>
    public bool HasCompany(Guid companyId) => IsAdmin || CompanyIds.Contains(companyId);
    public bool IsProvider => Ruc is not null;
    public bool CanRegister => Permissions.Contains(MenuCatalog.RegisterDocuments);
    /// <summary>Personal interno que registra documentos (Caja Chica y documentos especiales).</summary>
    public bool IsInternal => CanRegister && !IsProvider;
    public bool IsApprover => Permissions.Contains(MenuCatalog.Documents) && !IsProvider;
    public bool IsAccounting => Permissions.Contains(MenuCatalog.Accounting) && !IsProvider;
    public string Label => AreaName is null ? Name : $"{Name} · {AreaName}";
    public string RegistrationLabel => $"{Name} ({(IsProvider ? "proveedor" : "interno")})";
}
