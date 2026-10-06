using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Abstractions.Persistence;
using WebProveedores.Domain.Entities;

namespace WebProveedores.Infrastructure.Persistence;

public sealed class EfUserRepository(AppDbContext db) : IUserRepository
{
    public Task<AppUser?> FindForLoginAsync(string identifier, string normalizedEmail, CancellationToken cancellationToken) =>
        db.Users
            .Include(user => user.Emails)
            .Include(user => user.Area)
            .Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role)
            .SingleOrDefaultAsync(user => user.Username == identifier || user.Ruc == identifier || user.Emails.Any(email => email.Email == normalizedEmail && email.VerifiedAtUtc != null), cancellationToken);

    public Task<AppUser?> FindByRucAsync(string ruc, CancellationToken cancellationToken) =>
        db.Users.Include(user => user.Emails).Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role)
            .SingleOrDefaultAsync(user => user.Ruc == ruc, cancellationToken);

    public Task<AppUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        WithAccess(db.Users.AsNoTracking()).SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<AppUser?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken) =>
        WithAccess(db.Users).SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public async Task<UserSearchResult> SearchAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(user => user.Username.Contains(term) || user.CompanyName.Contains(term)
                || (user.Ruc != null && user.Ruc.Contains(term)) || user.Emails.Any(email => email.Email.Contains(term)));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await WithAccess(query)
            .OrderBy(user => user.CompanyName).ThenBy(user => user.Username)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        return new UserSearchResult(items, total);
    }

    public Task<bool> RucExistsAsync(string ruc, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(user => user.Ruc == ruc, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) =>
        db.UserEmails.AnyAsync(item => item.Email == email, cancellationToken);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(user => user.Username == username, cancellationToken);

    public Task<bool> EmailUsedByOtherAsync(string email, Guid exceptUserId, CancellationToken cancellationToken) =>
        db.UserEmails.AnyAsync(item => item.Email == email && item.UserId != exceptUserId, cancellationToken);

    public Task<bool> OtherActiveAdministratorExistsAsync(Guid exceptUserId, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(user => user.Id != exceptUserId && user.IsActive
            && user.UserRoles.Any(userRole => userRole.Role.Code == SecurityCatalog.AdministratorRole), cancellationToken);

    public Task<UserEmail?> FindEmailByVerificationTokenAsync(string tokenHash, DateTime now, CancellationToken cancellationToken) =>
        db.UserEmails.Include(email => email.User)
            .SingleOrDefaultAsync(email => email.VerificationTokenHash == tokenHash && email.VerificationExpiresAtUtc > now, cancellationToken);

    public void Add(AppUser user) => db.Users.Add(user);

    /// <summary>Correos, área, roles y sociedades: lo que define qué puede hacer el usuario.</summary>
    private static IQueryable<AppUser> WithAccess(IQueryable<AppUser> users) => users
        .Include(user => user.Emails)
        .Include(user => user.Area).ThenInclude(area => area!.Company)
        .Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role)
        .Include(user => user.UserCompanies).ThenInclude(userCompany => userCompany.Company);
}
