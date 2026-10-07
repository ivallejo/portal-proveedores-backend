using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Abstractions.Persistence;
using WebProveedores.Domain.Access;
using WebProveedores.Domain.Organization;

namespace WebProveedores.Infrastructure.Persistence;

public sealed class EfReferenceDataReader(AppDbContext db) : IReferenceDataReader
{
    public Task<Role?> FindRoleAsync(string code, CancellationToken cancellationToken) =>
        db.Roles.SingleOrDefaultAsync(role => role.Code == code, cancellationToken);

    public async Task<IReadOnlyList<Role>> ListRolesAsync(CancellationToken cancellationToken) =>
        await db.Roles.Where(role => role.IsActive).ToListAsync(cancellationToken);

    public Task<Area?> FindAreaAsync(Guid id, CancellationToken cancellationToken) =>
        db.Areas.Include(area => area.Company).SingleOrDefaultAsync(area => area.Id == id && area.IsActive, cancellationToken);

    public async Task<IReadOnlyList<Area>> ListActiveAreasAsync(CancellationToken cancellationToken) =>
        await db.Areas.Include(area => area.Company).Where(area => area.IsActive).OrderBy(area => area.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Company>> ListActiveCompaniesAsync(CancellationToken cancellationToken) =>
        await db.Companies.Where(company => company.IsActive).OrderBy(company => company.Code).ToListAsync(cancellationToken);
}
