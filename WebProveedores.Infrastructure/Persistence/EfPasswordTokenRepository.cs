using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Abstractions.Persistence;
using WebProveedores.Domain.Entities;

namespace WebProveedores.Infrastructure.Persistence;

public sealed class EfPasswordTokenRepository(AppDbContext db, TimeProvider clock) : IPasswordTokenRepository
{
    public Task<PasswordResetToken?> FindValidAsync(string ruc, string tokenHash, PasswordTokenPurpose purpose, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return db.PasswordResetTokens.Include(token => token.User).ThenInclude(user => user.Emails)
            .SingleOrDefaultAsync(token => token.User.Ruc == ruc && token.TokenHash == tokenHash && token.Purpose == purpose
                && token.UsedAtUtc == null && token.ExpiresAtUtc > now, cancellationToken);
    }

    public void Add(PasswordResetToken token) => db.PasswordResetTokens.Add(token);
}
