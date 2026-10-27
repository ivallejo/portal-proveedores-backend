using WebProveedores.Domain.Access;

namespace WebProveedores.Application.Common.Access;

/// <summary>
/// Usuario en sesión con lo que define su acceso: roles, permisos (opciones de menú de su rol) y sociedades asignadas.
/// El administrador trabaja con todas las sociedades.
/// </summary>
internal sealed record PortalActor(
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
    public bool IsProvider => Ruc is not null;
    public bool HasCompany(Guid companyId) => IsAdmin || CompanyIds.Contains(companyId);
    /// <summary>Tiene la opción de menú <paramref name="menuCode"/> (ver <see cref="MenuCatalog"/>).</summary>
    public bool Can(string menuCode) => Permissions.Contains(menuCode);
}
