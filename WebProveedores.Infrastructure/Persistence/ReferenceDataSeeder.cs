using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Entities;

namespace WebProveedores.Infrastructure.Persistence;

/// <summary>
/// Datos de referencia. Las sociedades se crean siempre (catálogo del negocio).
/// Las áreas y los usuarios de prueba por rol solo si <c>DemoData:Enabled</c> es verdadero
/// y hay una contraseña en <c>DemoData:Password</c>. Nunca modifica registros existentes.
/// </summary>
public sealed class ReferenceDataSeeder(AppDbContext db, IConfiguration configuration)
{
    private static readonly (string Code, string Name)[] Companies =
    [
        ("1001", "Naviera Transoceánica"),
        ("1002", "Ultratag"),
        ("1003", "Petral"),
        ("1007", "RENADSA"),
    ];

    private static readonly string[] Areas = ["Finanzas", "Logística", "Operaciones", "Mantenimiento", "Contabilidad"];

    private static readonly (string Username, string Name, string Email, string Role, string? Area, string? Ruc)[] DemoUsers =
    [
        ("colaborador", "Rocío Medina", "rmedina@naviera.test", SecurityCatalog.InternalUserRole, "Contabilidad", null),
        ("maria.torres", "María Torres", "mtorres@naviera.test", SecurityCatalog.AreaApproverRole, "Finanzas", null),
        ("jorge.paredes", "Jorge Paredes", "jparedes@naviera.test", SecurityCatalog.AreaApproverRole, "Finanzas", null),
        ("ana.rios", "Ana Ríos", "arios@naviera.test", SecurityCatalog.AreaApproverRole, "Logística", null),
        ("carlos.vega", "Carlos Vega", "cvega@naviera.test", SecurityCatalog.AreaApproverRole, "Operaciones", null),
        ("cxp", "Cuentas por pagar", "cxp@naviera.test", SecurityCatalog.AccountsPayableRole, "Contabilidad", null),
        ("20512345678", "Andes Suministros Industriales S.A.C.", "facturacion@andes.test", SecurityCatalog.ProviderRole, null, "20512345678"),
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var existingCompanies = await db.Companies.Select(company => company.Code).ToListAsync(cancellationToken);
        foreach (var (code, name) in Companies.Where(company => !existingCompanies.Contains(company.Code)))
            db.Companies.Add(new Company { Code = code, Name = name });
        await db.SaveChangesAsync(cancellationToken);

        var password = configuration["DemoData:Password"];
        if (!configuration.GetValue("DemoData:Enabled", false) || string.IsNullOrWhiteSpace(password)) return;

        var existingAreas = await db.Areas.ToDictionaryAsync(area => area.Name, cancellationToken);
        foreach (var name in Areas.Where(name => !existingAreas.ContainsKey(name)))
        {
            var area = new Area { Code = name.ToUpperInvariant().Replace("Í", "I"), Name = name };
            db.Areas.Add(area);
            existingAreas[name] = area;
        }

        var roles = await db.Roles.ToDictionaryAsync(role => role.Code, cancellationToken);
        var usernames = await db.Users.Select(user => user.Username).ToListAsync(cancellationToken);
        var hasher = new PasswordHasher<AppUser>();
        foreach (var demo in DemoUsers.Where(demo => !usernames.Contains(demo.Username)))
        {
            if (await db.UserEmails.AnyAsync(email => email.Email == demo.Email, cancellationToken)) continue;
            var user = new AppUser
            {
                Username = demo.Username,
                CompanyName = demo.Name,
                Ruc = demo.Ruc,
                Area = demo.Area is null ? null : existingAreas[demo.Area],
                PasswordSetAtUtc = DateTime.UtcNow,
            };
            user.PasswordHash = hasher.HashPassword(user, password);
            user.Emails.Add(new UserEmail { Email = demo.Email, IsPrimary = true });
            user.UserRoles.Add(new UserRole { RoleId = roles[demo.Role].Id });
            db.Users.Add(user);
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
