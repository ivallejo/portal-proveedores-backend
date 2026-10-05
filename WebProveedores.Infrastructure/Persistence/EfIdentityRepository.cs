using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Abstractions.Persistence;
using WebProveedores.Domain.Entities;

namespace WebProveedores.Infrastructure.Persistence;

public sealed class EfIdentityRepository(AppDbContext db) : IIdentityRepository
{
    public Task<AppUser?> FindForLoginAsync(string identifier, string normalizedEmail, CancellationToken cancellationToken) =>
        db.Users
            .Include(user => user.Emails)
            .Include(user => user.Area)
            .Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role)
            .SingleOrDefaultAsync(user => user.Username == identifier || user.Ruc == identifier || user.Emails.Any(email => email.Email == normalizedEmail), cancellationToken);

    public Task<AppUser?> FindByRucAsync(string ruc, CancellationToken cancellationToken) =>
        db.Users.Include(user => user.Emails).Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role)
            .SingleOrDefaultAsync(user => user.Ruc == ruc, cancellationToken);

    public Task<AppUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().Include(user => user.Emails).Include(user => user.Area).Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role).Include(user => user.UserCompanies).ThenInclude(userCompany => userCompany.Company)
            .SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<AppUser?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.Include(user => user.Emails).Include(user => user.Area).Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role).Include(user => user.UserCompanies).ThenInclude(userCompany => userCompany.Company)
            .SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<PasswordResetToken?> FindValidTokenAsync(string ruc, string tokenHash, PasswordTokenPurpose purpose, CancellationToken cancellationToken) =>
        db.PasswordResetTokens.Include(token => token.User).ThenInclude(user => user.Emails)
            .SingleOrDefaultAsync(token => token.User.Ruc == ruc && token.TokenHash == tokenHash && token.Purpose == purpose && token.UsedAtUtc == null && token.ExpiresAtUtc > DateTime.UtcNow, cancellationToken);

    public async Task<UserSearchResult> SearchUsersAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(user => user.Username.Contains(term) || user.CompanyName.Contains(term)
                || (user.Ruc != null && user.Ruc.Contains(term)) || user.Emails.Any(email => email.Email.Contains(term)));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(user => user.Emails)
            .Include(user => user.Area)
            .Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role)
            .Include(user => user.UserCompanies).ThenInclude(userCompany => userCompany.Company)
            .OrderBy(user => user.CompanyName).ThenBy(user => user.Username)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        return new UserSearchResult(items, total);
    }

    public async Task<IReadOnlyList<Domain.Documents.Company>> ListActiveCompaniesAsync(CancellationToken cancellationToken) =>
        await db.Companies.Where(company => company.IsActive).OrderBy(company => company.Code).ToListAsync(cancellationToken);

    public Task<bool> UserExistsByRucAsync(string ruc, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(user => user.Ruc == ruc, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) =>
        db.UserEmails.AnyAsync(item => item.Email == email, cancellationToken);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(user => user.Username == username, cancellationToken);

    public async Task<IReadOnlyList<Role>> ListRolesAsync(CancellationToken cancellationToken) =>
        await db.Roles.Where(role => role.IsActive).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Area>> ListActiveAreasAsync(CancellationToken cancellationToken) =>
        await db.Areas.AsNoTracking().Where(area => area.IsActive).OrderBy(area => area.Name).ToListAsync(cancellationToken);

    public Task<Area?> FindAreaAsync(Guid id, CancellationToken cancellationToken) =>
        db.Areas.SingleOrDefaultAsync(area => area.Id == id && area.IsActive, cancellationToken);

    public Task<bool> EmailUsedByOtherAsync(string email, Guid exceptUserId, CancellationToken cancellationToken) =>
        db.UserEmails.AnyAsync(item => item.Email == email && item.UserId != exceptUserId, cancellationToken);

    public Task<bool> OtherActiveAdministratorExistsAsync(Guid exceptUserId, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(user => user.Id != exceptUserId && user.IsActive
            && user.UserRoles.Any(userRole => userRole.Role.Code == SecurityCatalog.AdministratorRole), cancellationToken);

    public Task<Role?> FindRoleByCodeAsync(string code, CancellationToken cancellationToken) =>
        db.Roles.SingleOrDefaultAsync(role => role.Code == code, cancellationToken);

    public void AddUser(AppUser user) => db.Users.Add(user);

    public void AddPasswordToken(PasswordResetToken token) => db.PasswordResetTokens.Add(token);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) => await db.SaveChangesAsync(cancellationToken);
}
