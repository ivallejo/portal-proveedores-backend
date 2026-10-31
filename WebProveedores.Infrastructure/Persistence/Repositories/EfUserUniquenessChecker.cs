using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Ports.Outbound.Persistence;
using WebProveedores.Domain.Identity;

namespace WebProveedores.Infrastructure.Persistence.Repositories;

public sealed class EfUserUniquenessChecker(AppDbContext db) : IUserUniquenessChecker
{
    public Task<bool> DocumentExistsAsync(IdentityDocumentType type, string number, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(user => user.DocumentType == type && user.DocumentNumber == number, cancellationToken);

    public Task<bool> RucExistsAsync(string ruc, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(user => user.Ruc == ruc, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) =>
        db.UserEmails.AnyAsync(item => item.Email == email, cancellationToken);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(user => user.Username == username, cancellationToken);

    // ToLower: SQL Server ya compara sin mayúsculas (collation CI), pero así la regla no depende de la base.
    public Task<bool> UsernameUsedByOtherAsync(string username, Guid? exceptUserId, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(user => user.Username.ToLower() == username.ToLower() && user.Id != exceptUserId, cancellationToken);

    public Task<bool> EmailUsedByOtherAsync(string email, Guid exceptUserId, CancellationToken cancellationToken) =>
        db.UserEmails.AnyAsync(item => item.Email == email && item.UserId != exceptUserId, cancellationToken);
}
