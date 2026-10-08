using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Ports.Outbound.Persistence;
using WebProveedores.Application.Ports.Outbound.Persistence.Models;
using WebProveedores.Domain.Access;

namespace WebProveedores.Infrastructure.Persistence.Repositories;

public sealed class EfAccessRepository(AppDbContext db) : IAccessRepository
{
    public async Task<IReadOnlyList<MenuOption>> ListMenusAsync(CancellationToken cancellationToken) =>
        await db.MenuOptions.Include(menu => menu.RoleMenus).OrderBy(menu => menu.Order).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RoleSummary>> ListRolesAsync(CancellationToken cancellationToken)
    {
        var roles = await db.Roles.Include(role => role.RoleMenus).OrderBy(role => role.Name).ToListAsync(cancellationToken);
        var counts = await db.UserRoles.GroupBy(userRole => userRole.RoleId)
            .Select(group => new { RoleId = group.Key, Users = group.Count() })
            .ToDictionaryAsync(item => item.RoleId, item => item.Users, cancellationToken);
        return roles.Select(role => new RoleSummary(role, counts.GetValueOrDefault(role.Id))).ToList();
    }

    public void Add(Role role) => db.Roles.Add(role);
    public void Add(MenuOption menu) => db.MenuOptions.Add(menu);
}
