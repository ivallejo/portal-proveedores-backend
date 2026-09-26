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
        var email = configuration["BootstrapAdmin:Email"]?.Trim().ToLowerInvariant();
        var password = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        if (await db.Users.AnyAsync(user => user.Email == email, cancellationToken)) return;

        var admin = new AppUser { Email = email, CompanyName = configuration["BootstrapAdmin:CompanyName"]?.Trim() ?? "Administrador del sistema", Ruc = "ADMIN-SYSTEM", Role = "Administrador" };
        admin.PasswordHash = new PasswordHasher<AppUser>().HashPassword(admin, password);
        db.Users.Add(admin);
        await db.SaveChangesAsync(cancellationToken);
    }
}
