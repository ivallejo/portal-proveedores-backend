using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Ports.Outbound.Persistence;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Organization;

namespace WebProveedores.Infrastructure.Persistence.Repositories;

public sealed class EfOrganizationRepository(AppDbContext db) : IOrganizationRepository
{
    public Task<bool> CompanyCodeExistsAsync(string code, Guid? exceptId, CancellationToken cancellationToken) =>
        db.Companies.AnyAsync(company => company.Code == code && company.Id != exceptId, cancellationToken);

    public Task<bool> CompanyRucExistsAsync(string ruc, Guid? exceptId, CancellationToken cancellationToken) =>
        db.Companies.AnyAsync(company => company.Ruc == ruc && company.Id != exceptId, cancellationToken);

    public void AddCompany(Company company) => db.Companies.Add(company);

    public Task<bool> AreaCodeExistsAsync(string code, Guid? exceptId, CancellationToken cancellationToken) =>
        db.Areas.AnyAsync(area => area.Code == code && area.Id != exceptId, cancellationToken);

    public async Task<IReadOnlyList<string>> AreaUsersOutsideCompaniesAsync(Guid areaId, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken) =>
        await db.Users
            .Where(user => user.AreaId == areaId && !user.UserCompanies.Any(item => companyIds.Contains(item.CompanyId)))
            .OrderBy(user => user.CompanyName)
            .Select(user => user.CompanyName)
            .ToListAsync(cancellationToken);

    public Task<int> PendingApprovalsAsync(Guid areaId, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken) =>
        db.Documents.CountAsync(document => document.AreaId == areaId && companyIds.Contains(document.CompanyId)
            && document.Status == DocumentStatus.PendingApproval, cancellationToken);

    public void AddArea(Area area) => db.Areas.Add(area);
}
