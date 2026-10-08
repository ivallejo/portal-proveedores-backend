using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Ports.Outbound.Persistence;
using WebProveedores.Domain.Access;

namespace WebProveedores.Infrastructure.Persistence.Repositories;

public sealed class EfRoleReader(AppDbContext db) : IRoleReader
{
    public Task<Role?> FindRoleAsync(string code, CancellationToken cancellationToken) =>
        db.Roles.SingleOrDefaultAsync(role => role.Code == code, cancellationToken);

    public async Task<IReadOnlyList<Role>> ListRolesAsync(CancellationToken cancellationToken) =>
        await db.Roles.Where(role => role.IsActive).ToListAsync(cancellationToken);
}
