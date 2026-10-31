using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Ports.Outbound.Persistence;
using WebProveedores.Domain.Identity;

namespace WebProveedores.Infrastructure.Persistence.Repositories;

public sealed class EfUserRepository(AppDbContext db) : IUserRepository
{
    public Task<AppUser?> FindForLoginAsync(string username, CancellationToken cancellationToken) =>
        db.Users
            .Include(user => user.Emails)
            .Include(user => user.Area)
            .Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role)
            // El RUC es el usuario del proveedor (también para cuentas antiguas con otro usuario).
            .SingleOrDefaultAsync(user => user.Username == username || user.Ruc == username, cancellationToken);

    public Task<AppUser?> FindByRucAsync(string ruc, CancellationToken cancellationToken) =>
        db.Users.Include(user => user.Emails).Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role)
            .SingleOrDefaultAsync(user => user.Ruc == ruc, cancellationToken);

    public Task<AppUser?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken) =>
        UserIncludes.WithAccess(db.Users).Include(user => user.PasswordHistory).SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<UserEmail?> FindEmailByVerificationTokenAsync(string tokenHash, DateTime now, CancellationToken cancellationToken) =>
        db.UserEmails.Include(email => email.User)
            .SingleOrDefaultAsync(email => email.VerificationTokenHash == tokenHash && email.VerificationExpiresAtUtc > now, cancellationToken);

    public void Add(AppUser user) => db.Users.Add(user);
}
