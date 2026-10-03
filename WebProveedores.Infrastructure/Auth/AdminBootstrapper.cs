using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WebProveedores.Domain.Entities;
using WebProveedores.Infrastructure.Persistence;

namespace WebProveedores.Infrastructure.Auth;

public sealed class AdminBootstrapper(AppDbContext db, IConfiguration configuration)
{
    public async Task EnsureAdminAsync(CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync(cancellationToken);
        var email = configuration["BootstrapAdmin:Email"]?.Trim().ToLowerInvariant();
        var password = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        var existingAdmin = await db.Users.Include(user => user.Emails).Include(user => user.UserRoles).SingleOrDefaultAsync(user => user.Emails.Any(item => item.Email == email), cancellationToken);
        var adminRole = await db.Roles.SingleAsync(role => role.Code == SecurityCatalog.AdministratorRole, cancellationToken);
        if (existingAdmin is not null)
        {
            if (!existingAdmin.UserRoles.Any(userRole => userRole.RoleId == adminRole.Id)) existingAdmin.UserRoles.Add(new UserRole { UserId = existingAdmin.Id, RoleId = adminRole.Id });
            if (string.IsNullOrWhiteSpace(existingAdmin.Username)) existingAdmin.Username = "admin";
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var admin = new AppUser { Username = "admin", CompanyName = configuration["BootstrapAdmin:CompanyName"]?.Trim() ?? "Administrador del sistema", PasswordSetAtUtc = DateTime.UtcNow };
        admin.PasswordHash = new PasswordHasher<AppUser>().HashPassword(admin, password);
        admin.Emails.Add(new UserEmail { Email = email, IsPrimary = true });
        admin.UserRoles.Add(new UserRole { RoleId = adminRole.Id });
        db.Users.Add(admin);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        var existingCodes = await db.Roles.Select(role => role.Code).ToListAsync(cancellationToken);
        foreach (var role in SecurityCatalog.Roles.Where(role => !existingCodes.Contains(role.Key)))
            db.Roles.Add(new Role { Code = role.Key, Name = role.Value });
        await db.SaveChangesAsync(cancellationToken);
    }
}
