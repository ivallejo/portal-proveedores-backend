using System.Text.RegularExpressions;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Application.Contracts.Access.Commands;
using WebProveedores.Application.Contracts.Access.Responses;
using WebProveedores.Application.Ports.Inbound.Access;
using WebProveedores.Application.Ports.Outbound.Persistence;
using WebProveedores.Application.Ports.Outbound.Persistence.Models;
using WebProveedores.Domain.Access;
using WebProveedores.Domain.Organization;

namespace WebProveedores.Application.UseCases.Access;

/// <summary>
/// Roles (nombre, descripción y opciones de menú) y opciones de menú (dos niveles). Reglas que evitan quedarse sin
/// acceso: el rol Administrador no se desactiva y conserva Configuración › Roles y permisos y Menús; esas opciones
/// tampoco se desactivan. Las opciones del sistema conservan su código, su ruta y su nivel.
/// </summary>
internal sealed partial class AccessAdminService(IAccessRepository access, IUnitOfWork unitOfWork, TimeProvider clock) : IAccessAdminService
{
    // ——— Roles ———

    public async Task<IReadOnlyList<RoleAdminResponse>> ListRolesAsync(CancellationToken cancellationToken) =>
        (await access.ListRolesAsync(cancellationToken))
            .OrderBy(item => item.Role.IsSystem ? SecurityCatalog.Roles.Keys.ToList().IndexOf(item.Role.Code) : int.MaxValue)
            .ThenBy(item => item.Role.Name)
            .Select(item => ToResponse(item.Role, item.UserCount)).ToArray();

    public async Task<RoleAdminResponse> CreateRoleAsync(SaveRoleCommand request, CancellationToken cancellationToken)
    {
        var roles = await access.ListRolesAsync(cancellationToken);
        var name = RoleName(request, roles, null);
        var menus = await access.ListMenusAsync(cancellationToken);
        var code = UniqueCode(Area.CodeFor(name), roles.Select(item => item.Role.Code));
        var role = new Role { Code = code, Name = name, Description = Description(request) };
        SetMenus(role, request.MenuIds, menus);
        access.Add(role);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(role, 0);
    }

    public async Task<RoleAdminResponse> UpdateRoleAsync(Guid id, SaveRoleCommand request, CancellationToken cancellationToken)
    {
        var roles = await access.ListRolesAsync(cancellationToken);
        var current = roles.FirstOrDefault(item => item.Role.Id == id) ?? throw new NotFoundException("El rol no existe.");
        var menus = await access.ListMenusAsync(cancellationToken);
        current.Role.Name = RoleName(request, roles, id);
        current.Role.Description = Description(request);
        SetMenus(current.Role, request.MenuIds, menus);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(current.Role, current.UserCount);
    }

    public async Task<RoleAdminResponse> SetRoleStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var current = (await access.ListRolesAsync(cancellationToken)).FirstOrDefault(item => item.Role.Id == id)
            ?? throw new NotFoundException("El rol no existe.");
        if (!isActive && current.Role.Code == SecurityCatalog.AdministratorRole)
            throw new ConflictException("El rol Administrador no se puede desactivar.");
        current.Role.IsActive = isActive;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(current.Role, current.UserCount);
    }

    // ——— Menús ———

    public async Task<IReadOnlyList<MenuAdminResponse>> ListMenusAsync(CancellationToken cancellationToken)
    {
        var menus = await access.ListMenusAsync(cancellationToken);
        // Primero cada menú principal y debajo sus submenús.
        return menus.Where(menu => menu.ParentId is null).OrderBy(menu => menu.Order).ThenBy(menu => menu.Name)
            .SelectMany(parent => new[] { parent }.Concat(menus.Where(child => child.ParentId == parent.Id).OrderBy(child => child.Order).ThenBy(child => child.Name)))
            .Select(ToResponse).ToArray();
    }

    public async Task<MenuAdminResponse> CreateMenuAsync(SaveMenuCommand request, CancellationToken cancellationToken)
    {
        var menus = await access.ListMenusAsync(cancellationToken);
        var menu = new MenuOption
        {
            Code = UniqueCode("MENU_" + Area.CodeFor(request.Name.Trim()), menus.Select(item => item.Code)),
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };
        Apply(menu, request, menus);
        // Una opción nueva la ve el administrador; luego se asigna a otros roles en Roles y permisos.
        var administrator = (await access.ListRolesAsync(cancellationToken)).FirstOrDefault(item => item.Role.Code == SecurityCatalog.AdministratorRole)?.Role;
        if (administrator is not null) menu.RoleMenus.Add(new RoleMenu { Role = administrator, RoleId = administrator.Id, MenuOption = menu, MenuOptionId = menu.Id });
        if (menu.ParentId is { } parentId && administrator is not null && administrator.RoleMenus.All(item => item.MenuOptionId != parentId))
            administrator.RoleMenus.Add(new RoleMenu { RoleId = administrator.Id, MenuOptionId = parentId });
        access.Add(menu);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(menu);
    }

    public async Task<MenuAdminResponse> UpdateMenuAsync(Guid id, SaveMenuCommand request, CancellationToken cancellationToken)
    {
        var menus = await access.ListMenusAsync(cancellationToken);
        var menu = menus.FirstOrDefault(item => item.Id == id) ?? throw new NotFoundException("La opción de menú no existe.");
        Apply(menu, request, menus);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(menu);
    }

    public async Task<MenuAdminResponse> SetMenuStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var menus = await access.ListMenusAsync(cancellationToken);
        var menu = menus.FirstOrDefault(item => item.Id == id) ?? throw new NotFoundException("La opción de menú no existe.");
        if (!isActive && MenuCatalog.Protected.Contains(menu.Code))
            throw new ConflictException($"«{menu.Name}» no se puede desactivar: sin ella nadie podría administrar roles y menús.");
        menu.IsActive = isActive;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(menu);
    }

    // ——— Reglas ———

    private static string RoleName(SaveRoleCommand request, IReadOnlyList<RoleSummary> roles, Guid? exceptId)
    {
        var name = request.Name.Trim();
        if (name.Length == 0) throw new ValidationException("Ingresa el nombre del rol.");
        if (roles.Any(item => item.Role.Id != exceptId && string.Equals(item.Role.Name, name, StringComparison.CurrentCultureIgnoreCase)))
            throw new ConflictException("Ya existe un rol con ese nombre.");
        return name;
    }

    private static string? Description(SaveRoleCommand request) => string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

    /// <summary>
    /// Deja exactamente estas opciones. Un submenú arrastra a su menú principal; un menú principal que agrupa
    /// submenús solo queda si se eligió alguno de ellos.
    /// </summary>
    private static void SetMenus(Role role, IReadOnlyList<Guid> menuIds, IReadOnlyList<MenuOption> menus)
    {
        var chosen = menuIds.Distinct().Select(id => menus.FirstOrDefault(menu => menu.Id == id) ?? throw new ValidationException("Una de las opciones de menú no existe.")).ToList();
        var wanted = chosen.Where(menu => menu.ParentId is not null || menus.All(child => child.ParentId != menu.Id)).Select(menu => menu.Id).ToHashSet();
        foreach (var child in chosen.Where(menu => menu.ParentId is not null)) wanted.Add(child.ParentId!.Value);
        if (wanted.Count == 0) throw new ValidationException("Selecciona al menos una opción del menú.");

        if (role.Code == SecurityCatalog.AdministratorRole)
        {
            var missing = menus.Where(menu => MenuCatalog.Protected.Contains(menu.Code) && !wanted.Contains(menu.Id)).Select(menu => menu.Name).ToArray();
            if (missing.Length > 0)
                throw new ValidationException($"El rol Administrador debe conservar: {string.Join(", ", missing)}.");
        }

        foreach (var stale in role.RoleMenus.Where(item => !wanted.Contains(item.MenuOptionId)).ToList()) role.RoleMenus.Remove(stale);
        foreach (var id in wanted.Where(id => role.RoleMenus.All(item => item.MenuOptionId != id)))
            role.RoleMenus.Add(new RoleMenu { RoleId = role.Id, MenuOptionId = id });
    }

    private static void Apply(MenuOption menu, SaveMenuCommand request, IReadOnlyList<MenuOption> menus)
    {
        var name = request.Name.Trim();
        if (name.Length == 0) throw new ValidationException("Ingresa el nombre de la opción.");
        var icon = request.Icon.Trim();
        if (icon.Length == 0) throw new ValidationException("Elige un ícono.");
        if (request.Order is < 1 or > 99) throw new ValidationException("El orden debe estar entre 1 y 99.");

        var parentId = request.ParentId;
        if (menu.IsSystem)
        {
            // Las opciones del sistema enlazan a pantallas del portal: su nivel y su ruta no cambian.
            if (parentId != menu.ParentId) throw new ValidationException("Una opción del sistema no se puede mover a otro menú.");
            if (!string.Equals(Normalize(request.Route), menu.Route, StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("La ruta de una opción del sistema no se puede cambiar.");
        }
        else
        {
            if (parentId is { } id)
            {
                var parent = menus.FirstOrDefault(item => item.Id == id) ?? throw new ValidationException("El menú principal no existe.");
                if (parent.Id == menu.Id || parent.ParentId is not null) throw new ValidationException("Solo hay dos niveles: elige un menú principal.");
                if (menus.Any(item => item.ParentId == menu.Id)) throw new ValidationException("Esta opción tiene submenús: no puede ser un submenú.");
            }
            var route = Normalize(request.Route);
            if (route is null && parentId is not null) throw new ValidationException("Un submenú necesita una ruta.");
            if (route is not null)
            {
                if (!RoutePattern().IsMatch(route)) throw new ValidationException("La ruta debe empezar con / y usar letras minúsculas, números, guiones o /.");
                if (menus.Any(item => item.Id != menu.Id && string.Equals(item.Route, route, StringComparison.OrdinalIgnoreCase)))
                    throw new ConflictException("Ya existe una opción con esa ruta.");
            }
            menu.Route = route;
            menu.ParentId = parentId;
        }

        if (menus.Any(item => item.Id != menu.Id && item.ParentId == parentId && string.Equals(item.Name, name, StringComparison.CurrentCultureIgnoreCase)))
            throw new ConflictException("Ya existe una opción con ese nombre en el mismo menú.");
        menu.Name = name;
        menu.Icon = icon;
        menu.Order = request.Order;
    }

    private static string? Normalize(string? route) => string.IsNullOrWhiteSpace(route) ? null : route.Trim().TrimEnd('/') is { Length: > 0 } trimmed ? trimmed : "/";

    private static string UniqueCode(string baseCode, IEnumerable<string> existing)
    {
        var taken = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var code = baseCode.Length > 40 ? baseCode[..40] : baseCode;
        for (var suffix = 2; taken.Contains(code); suffix++) code = $"{baseCode[..Math.Min(baseCode.Length, 37)]}_{suffix}";
        return code;
    }

    private static RoleAdminResponse ToResponse(Role role, int userCount) =>
        new(role.Id, role.Code, role.Name, role.Description, role.IsActive, role.IsSystem, userCount, role.RoleMenus.Select(item => item.MenuOptionId).ToArray());

    private static MenuAdminResponse ToResponse(MenuOption menu) =>
        new(menu.Id, menu.Code, menu.Name, menu.Route, menu.Icon, menu.Order, menu.ParentId, menu.IsActive, menu.IsSystem, menu.RoleMenus.Count);

    [GeneratedRegex("^/[a-z0-9-]+(/[a-z0-9-]+)*$")]
    private static partial Regex RoutePattern();
}
