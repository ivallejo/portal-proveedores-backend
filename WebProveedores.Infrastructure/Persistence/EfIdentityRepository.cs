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

    public async Task<IReadOnlyList<AppUser>> ListUsersAsync(CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking().Include(user => user.Emails).Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role).Include(user => user.UserCompanies).ThenInclude(userCompany => userCompany.Company)
            .OrderBy(user => user.CompanyName).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Domain.Documents.Company>> ListActiveCompaniesAsync(CancellationToken cancellationToken) =>
        await db.Companies.Where(company => company.IsActive).OrderBy(company => company.Code).ToListAsync(cancellationToken);

    public Task<bool> UserExistsByRucAsync(string ruc, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(user => user.Ruc == ruc, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) =>
        db.UserEmails.AnyAsync(item => item.Email == email, cancellationToken);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(user => user.Username == username, cancellationToken);

    public Task<Role?> FindRoleAsync(string value, CancellationToken cancellationToken) =>
        db.Roles.SingleOrDefaultAsync(role => role.Code == value || role.Name == value, cancellationToken);

    public Task<Role?> FindRoleByCodeAsync(string code, CancellationToken cancellationToken) =>
        db.Roles.SingleOrDefaultAsync(role => role.Code == code, cancellationToken);

    public void AddUser(AppUser user) => db.Users.Add(user);

    public void AddPasswordToken(PasswordResetToken token) => db.PasswordResetTokens.Add(token);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) => await db.SaveChangesAsync(cancellationToken);
}
