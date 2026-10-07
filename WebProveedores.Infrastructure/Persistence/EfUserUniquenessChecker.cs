using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Ports.Outbound.Persistence;

namespace WebProveedores.Infrastructure.Persistence;

public sealed class EfUserUniquenessChecker(AppDbContext db) : IUserUniquenessChecker
{
    public Task<bool> DniExistsAsync(string dni, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(user => user.Dni == dni || user.Username == dni, cancellationToken);

    public Task<bool> RucExistsAsync(string ruc, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(user => user.Ruc == ruc, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) =>
        db.UserEmails.AnyAsync(item => item.Email == email, cancellationToken);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(user => user.Username == username, cancellationToken);

    public Task<bool> EmailUsedByOtherAsync(string email, Guid exceptUserId, CancellationToken cancellationToken) =>
        db.UserEmails.AnyAsync(item => item.Email == email && item.UserId != exceptUserId, cancellationToken);
}
