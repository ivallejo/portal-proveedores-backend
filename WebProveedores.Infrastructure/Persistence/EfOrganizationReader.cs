using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Ports.Outbound.Persistence;
using WebProveedores.Application.Ports.Outbound.Persistence.Models;
using WebProveedores.Domain.Organization;

namespace WebProveedores.Infrastructure.Persistence;

public sealed class EfOrganizationReader(AppDbContext db) : IOrganizationReader
{
    public async Task<IReadOnlyList<CompanySummary>> ListCompaniesAsync(CancellationToken cancellationToken) =>
        (await db.Companies
            .Select(company => new
            {
                Company = company,
                Areas = db.Areas.Count(area => area.CompanyId == company.Id),
                Users = db.Set<UserCompany>().Count(item => item.CompanyId == company.Id),
            })
            .ToListAsync(cancellationToken))
        .Select(row => new CompanySummary(row.Company, row.Areas, row.Users))
        .ToArray();

    public Task<Company?> FindCompanyAsync(Guid id, CancellationToken cancellationToken) =>
        db.Companies.SingleOrDefaultAsync(company => company.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AreaSummary>> ListAreasAsync(CancellationToken cancellationToken) =>
        (await db.Areas
            .Include(area => area.Company)
            .Select(area => new { Area = area, Users = db.Users.Count(user => user.AreaId == area.Id) })
            .ToListAsync(cancellationToken))
        .Select(row => new AreaSummary(row.Area, row.Users))
        .ToArray();

    public Task<Area?> FindAreaAsync(Guid id, CancellationToken cancellationToken) =>
        db.Areas.Include(area => area.Company).SingleOrDefaultAsync(area => area.Id == id, cancellationToken);
}
