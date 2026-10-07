using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Ports.Outbound.Persistence;
using WebProveedores.Domain.Identity;

namespace WebProveedores.Infrastructure.Persistence;

public sealed class EfPasswordTokenRepository(AppDbContext db, TimeProvider clock) : IPasswordTokenRepository
{
    public Task<PasswordResetToken?> FindValidAsync(string tokenHash, PasswordTokenPurpose purpose, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return db.PasswordResetTokens.Include(token => token.User).ThenInclude(user => user.Emails)
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash && token.Purpose == purpose
                && token.UsedAtUtc == null && token.RevokedAtUtc == null && token.ExpiresAtUtc > now, cancellationToken);
    }

    public async Task<IReadOnlyList<PasswordResetToken>> ListForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.PasswordResetTokens.Where(token => token.UserId == userId).OrderByDescending(token => token.CreatedAtUtc).ToListAsync(cancellationToken);

    public void Add(PasswordResetToken token) => db.PasswordResetTokens.Add(token);
}
