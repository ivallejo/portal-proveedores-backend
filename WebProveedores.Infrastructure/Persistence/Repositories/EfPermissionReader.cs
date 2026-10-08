using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Ports.Outbound.Persistence;

namespace WebProveedores.Infrastructure.Persistence.Repositories;

public sealed class EfPermissionReader(AppDbContext db) : IPermissionReader
{
    public async Task<IReadOnlySet<string>> PermissionsOfAsync(Guid userId, CancellationToken cancellationToken) =>
        (await db.UserRoles
            .Where(userRole => userRole.UserId == userId && userRole.Role.IsActive)
            .SelectMany(userRole => userRole.Role.RoleMenus)
            .Where(item => item.MenuOption.IsActive && (item.MenuOption.ParentId == null || item.MenuOption.Parent!.IsActive))
            .Select(item => item.MenuOption.Code)
            .Distinct()
            .ToListAsync(cancellationToken)).ToHashSet();
}
