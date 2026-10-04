using Microsoft.EntityFrameworkCore;
using WebProveedores.Domain.Entities;

namespace WebProveedores.Application.Abstractions.Persistence;

public interface IAppDbContext
{
    DbSet<AppUser> Users { get; }
    DbSet<Area> Areas { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserEmail> UserEmails { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
