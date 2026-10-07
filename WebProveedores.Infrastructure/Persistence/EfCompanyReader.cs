using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Ports.Outbound.Persistence;
using WebProveedores.Domain.Organization;

namespace WebProveedores.Infrastructure.Persistence;

public sealed class EfCompanyReader(AppDbContext db) : ICompanyReader
{
    public Task<Company?> FindByCodeAsync(string code, CancellationToken cancellationToken) =>
        db.Companies.SingleOrDefaultAsync(company => company.Code == code, cancellationToken);

    public async Task<IReadOnlyList<Company>> ListActiveAsync(CancellationToken cancellationToken) =>
        await db.Companies.Where(company => company.IsActive).OrderBy(company => company.Code).ToListAsync(cancellationToken);
}
