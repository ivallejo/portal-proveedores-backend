using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Ports.Outbound.Persistence;
using WebProveedores.Domain.Organization;

namespace WebProveedores.Infrastructure.Persistence.Repositories;

public sealed class EfOrganizationRepository(AppDbContext db) : IOrganizationRepository
{
    public Task<bool> CompanyCodeExistsAsync(string code, Guid? exceptId, CancellationToken cancellationToken) =>
        db.Companies.AnyAsync(company => company.Code == code && company.Id != exceptId, cancellationToken);

    public Task<bool> CompanyRucExistsAsync(string ruc, Guid? exceptId, CancellationToken cancellationToken) =>
        db.Companies.AnyAsync(company => company.Ruc == ruc && company.Id != exceptId, cancellationToken);

    public void AddCompany(Company company) => db.Companies.Add(company);

    public Task<bool> AreaCodeExistsAsync(Guid companyId, string code, Guid? exceptId, CancellationToken cancellationToken) =>
        db.Areas.AnyAsync(area => area.CompanyId == companyId && area.Code == code && area.Id != exceptId, cancellationToken);

    public void AddArea(Area area) => db.Areas.Add(area);
}
