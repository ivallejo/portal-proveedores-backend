using WebProveedores.Domain.Entities;

namespace WebProveedores.Application.Documents;

/// <summary>Quién actúa sobre los documentos: roles, área y sociedades del usuario en sesión.</summary>
internal sealed record DocumentActor(Guid Id, string Name, string Email, string? Ruc, Guid? AreaId, string? AreaName, IReadOnlySet<string> Roles, IReadOnlySet<Guid> CompanyIds)
{
    public bool IsAdmin => Roles.Contains(SecurityCatalog.AdministratorRole);
    /// <summary>El administrador trabaja con todas las sociedades.</summary>
    public bool HasCompany(Guid companyId) => IsAdmin || CompanyIds.Contains(companyId);
    public bool IsProvider => Roles.Contains(SecurityCatalog.ProviderRole);
    public bool IsInternal => Roles.Contains(SecurityCatalog.InternalUserRole);
    public bool IsApprover => Roles.Contains(SecurityCatalog.AreaApproverRole);
    public bool IsAccounting => Roles.Contains(SecurityCatalog.AccountsPayableRole);
    public bool CanRegister => IsAdmin || IsProvider || IsInternal;
    public string Label => AreaName is null ? Name : $"{Name} · {AreaName}";
    public string RegistrationLabel => $"{Name} ({(IsProvider ? "proveedor" : "interno")})";
}
