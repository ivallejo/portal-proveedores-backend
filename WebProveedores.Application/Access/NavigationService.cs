using WebProveedores.Application.Ports.Outbound.Persistence;

namespace WebProveedores.Application.Access;

/// <summary>Arma el menú lateral con las opciones que el rol del usuario tiene asignadas.</summary>
internal sealed class NavigationService(IAccessRepository access) : INavigationService
{
    public async Task<IReadOnlyList<NavigationItem>> MenuForAsync(Guid userId, CancellationToken cancellationToken)
    {
        var allowed = await access.PermissionsOfAsync(userId, cancellationToken);
        var menus = (await access.ListMenusAsync(cancellationToken)).Where(menu => menu.IsActive && allowed.Contains(menu.Code)).ToList();
        return menus.Where(menu => menu.ParentId is null)
            .OrderBy(menu => menu.Order).ThenBy(menu => menu.Name)
            .Select(parent => new NavigationItem(parent.Code, parent.Name, parent.Route, parent.Icon,
                menus.Where(child => child.ParentId == parent.Id).OrderBy(child => child.Order).ThenBy(child => child.Name)
                    .Select(child => new NavigationItem(child.Code, child.Name, child.Route, child.Icon, [])).ToArray()))
            // Un menú principal sin ruta solo agrupa: si no le queda ningún submenú, no se muestra.
            .Where(item => item.Route is not null || item.Children.Count > 0)
            .ToArray();
    }
}
