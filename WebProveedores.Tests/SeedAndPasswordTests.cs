using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using WebProveedores.Application.Abstractions.Auth;
using WebProveedores.Application.Auth;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Entities;
using WebProveedores.Infrastructure.Persistence;

namespace WebProveedores.Tests;

public sealed class SeedAndPasswordTests : IDisposable
{
    private readonly string seedPath = Path.Combine(Path.GetTempPath(), $"seed-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(seedPath)) File.Delete(seedPath);
    }

    [Fact]
    public async Task Seed_creates_companies_areas_and_users_with_temporary_passwords_and_is_idempotent()
    {
        await using var db = CreateContext();
        await File.WriteAllTextAsync(seedPath, ValidSeed);
        var seeder = CreateSeeder(db, "Seed:TemporaryPassword", "Temporal_Comun_1");

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        var approver = await db.Users.Include(user => user.Area).Include(user => user.UserRoles).ThenInclude(item => item.Role).SingleAsync(user => user.Username == "maria.torres");
        Assert.Equal("Finanzas", approver.Area!.Name);
        Assert.Equal(SecurityCatalog.AreaApproverRole, approver.UserRoles.Single().Role.Code);
        Assert.True(approver.MustChangePassword);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<AppUser>().VerifyHashedPassword(approver, approver.PasswordHash, "Temporal_Comun_1"));
        var own = await db.Users.SingleAsync(user => user.Username == "rocio.medina");
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<AppUser>().VerifyHashedPassword(own, own.PasswordHash, "Propia_Clave_9"));
        Assert.Equal(2, await db.Users.CountAsync());
        Assert.Equal("20000000001", (await db.Companies.SingleAsync(company => company.Code == "1001")).Ruc);
        Assert.Equal(4, await db.Companies.CountAsync());
        Assert.Equal(SecurityCatalog.Roles.Count, await db.Roles.CountAsync());
        Assert.Equal(2, await db.Areas.CountAsync());
        // «companies» limita las sociedades; si se omite, el usuario trabaja con todas.
        Assert.Equal(["1001", "1003"], await db.Set<UserCompany>().Where(item => item.UserId == approver.Id).Select(item => item.Company.Code).OrderBy(code => code).ToListAsync());
        Assert.Equal(4, await db.Set<UserCompany>().CountAsync(item => item.UserId == own.Id));
    }

    [Fact]
    public async Task Seed_never_modifies_existing_users_or_overwrites_a_company_ruc()
    {
        await using var db = CreateContext();
        db.Roles.AddRange(SecurityCatalog.Roles.Select(role => new Role { Code = role.Key, Name = role.Value }));
        db.Companies.Add(new Company { Code = "1001", Name = "Nombre propio", Ruc = "20999999999" });
        var existing = new AppUser { Username = "maria.torres", CompanyName = "Nombre original", PasswordHash = "hash-original" };
        existing.Emails.Add(new UserEmail { Email = "otra@correo.test", IsPrimary = true });
        db.Users.Add(existing);
        await db.SaveChangesAsync();
        await File.WriteAllTextAsync(seedPath, ValidSeed);

        await CreateSeeder(db, "Seed:TemporaryPassword", "Temporal_Comun_1").SeedAsync();

        var user = await db.Users.SingleAsync(item => item.Username == "maria.torres");
        Assert.Equal("Nombre original", user.CompanyName);
        Assert.Equal("hash-original", user.PasswordHash);
        Assert.False(user.MustChangePassword);
        Assert.Equal("20999999999", (await db.Companies.SingleAsync(company => company.Code == "1001")).Ruc);
    }

    [Fact]
    public async Task Seed_fails_fast_with_every_problem_listed_and_creates_no_users()
    {
        await using var db = CreateContext();
        await File.WriteAllTextAsync(seedPath, """
            { "areas": ["Finanzas"],
              "users": [
                { "username": "a", "name": "A", "email": "sin-arroba", "role": "INVENTADO" },
                { "username": "b", "name": "B", "email": "b@x.test", "role": "AREA_APPROVER", "area": "Otra", "temporaryPassword": "corta" },
                { "username": "c", "name": "C", "email": "c@x.test", "role": "PROVIDER", "ruc": "123", "temporaryPassword": "Valida_Clave_1" } ] }
            """);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSeeder(db).SeedAsync());

        Assert.Contains("rol desconocido", exception.Message);
        Assert.Contains("correo válido", exception.Message);
        Assert.Contains("«Otra»", exception.Message);
        Assert.Contains("contraseña temporal", exception.Message);
        Assert.Contains("RUC de 11 dígitos", exception.Message);
        Assert.Empty(db.Users);
    }

    [Fact]
    public async Task Without_a_seed_file_only_the_roles_and_base_companies_are_created()
    {
        await using var db = CreateContext();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:FilePath"] = null }).Build();
        var originalDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(Path.GetTempPath());
        try { await new ReferenceDataSeeder(db, configuration, NullLogger<ReferenceDataSeeder>.Instance).SeedAsync(); }
        finally { Directory.SetCurrentDirectory(originalDirectory); }

        Assert.Equal(4, await db.Companies.CountAsync());
        Assert.Empty(db.Users);
    }

    [Fact]
    public async Task Login_with_a_temporary_password_flags_the_session_and_changing_it_issues_a_clean_one()
    {
        await using var db = CreateContext();
        var user = await AddUserAsync(db, mustChange: true);
        var service = CreateAuth(db);

        var login = await service.LoginAsync(new LoginRequest { Identifier = "temporal", Password = "Password1" }, CancellationToken.None);
        Assert.NotNull(login);
        Assert.True(login.User.MustChangePassword);
        Assert.Contains(new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken).Claims, claim => claim.Type == AuthService.PasswordChangeClaim);

        // La sesión de cambio forzado no vuelve a pedir la contraseña temporal.
        var changed = await service.ChangePasswordAsync(Principal(user, forcedChange: true), new ChangePasswordRequest { NewPassword = "Nueva_Clave_2" }, CancellationToken.None);

        Assert.False(changed.User.MustChangePassword);
        Assert.DoesNotContain(new JwtSecurityTokenHandler().ReadJwtToken(changed.AccessToken).Claims, claim => claim.Type == AuthService.PasswordChangeClaim);
        Assert.Null(await service.LoginAsync(new LoginRequest { Identifier = "temporal", Password = "Password1" }, CancellationToken.None));
        Assert.NotNull(await service.LoginAsync(new LoginRequest { Identifier = "temporal", Password = "Nueva_Clave_2" }, CancellationToken.None));
    }

    [Fact]
    public async Task Forced_change_still_rejects_reusing_the_temporary_password_and_voluntary_change_needs_the_current_one()
    {
        await using var db = CreateContext();
        var user = await AddUserAsync(db, mustChange: true);
        var service = CreateAuth(db);

        var reused = await Assert.ThrowsAsync<ArgumentException>(() => service.ChangePasswordAsync(Principal(user, forcedChange: true), new ChangePasswordRequest { NewPassword = "Password1" }, CancellationToken.None));
        Assert.Contains("distinta", reused.Message);
        // Sin la sesión de cambio forzado (cambio voluntario) la contraseña actual es obligatoria.
        await Assert.ThrowsAsync<ArgumentException>(() => service.ChangePasswordAsync(Principal(user), new ChangePasswordRequest { NewPassword = "Nueva_Clave_2" }, CancellationToken.None));
        Assert.True(user.MustChangePassword);
    }

    [Theory]
    [InlineData("Incorrecta_1", "Nueva_Clave_2", "actual no es correcta")]
    [InlineData("Password1", "Password1", "distinta")]
    [InlineData("Password1", "sinmayuscula1", "mayúscula")]
    [InlineData("Password1", "Corta1a", "")]
    public async Task ChangePassword_rejects_invalid_requests_and_keeps_the_old_password(string current, string next, string expected)
    {
        await using var db = CreateContext();
        var user = await AddUserAsync(db, mustChange: true);
        var service = CreateAuth(db);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.ChangePasswordAsync(Principal(user), new ChangePasswordRequest { CurrentPassword = current, NewPassword = next }, CancellationToken.None));

        Assert.Contains(expected, exception.Message);
        Assert.True(user.MustChangePassword);
        Assert.NotNull(await service.LoginAsync(new LoginRequest { Identifier = "temporal", Password = "Password1" }, CancellationToken.None));
    }

    private const string ValidSeed = """
        {
          // comentario permitido
          "companies": [ { "code": "1001", "ruc": "20000000001" } ],
          "areas": ["Finanzas", "Logística"],
          "users": [
            { "username": "maria.torres", "name": "María Torres", "email": "maria@ejemplo.test", "role": "AREA_APPROVER", "area": "Finanzas", "companies": ["1001", "1003"] },
            { "username": "rocio.medina", "name": "Rocío Medina", "email": "rocio@ejemplo.test", "role": "INTERNAL_USER", "temporaryPassword": "Propia_Clave_9" }
          ]
        }
        """;

    private ReferenceDataSeeder CreateSeeder(AppDbContext db, string? key = null, string? value = null)
    {
        if (!db.Roles.Any())
        {
            db.Roles.AddRange(SecurityCatalog.Roles.Select(role => new Role { Code = role.Key, Name = role.Value }));
            db.SaveChanges();
        }
        var settings = new Dictionary<string, string?> { ["Seed:FilePath"] = seedPath };
        if (key is not null) settings[key] = value;
        return new ReferenceDataSeeder(db, new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), NullLogger<ReferenceDataSeeder>.Instance);
    }

    private static AuthService CreateAuth(AppDbContext db) => new(
        new EfIdentityRepository(db),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = "test-signing-key-with-at-least-32-characters",
            ["Jwt:Issuer"] = "test-issuer",
            ["Jwt:Audience"] = "test-audience",
            ["Frontend:BaseUrl"] = "http://localhost:4200",
        }).Build(),
        new NoEmail());

    private static async Task<AppUser> AddUserAsync(AppDbContext db, bool mustChange)
    {
        var user = new AppUser { Username = "temporal", CompanyName = "Usuario Temporal", MustChangePassword = mustChange };
        user.PasswordHash = new PasswordHasher<AppUser>().HashPassword(user, "Password1");
        user.Emails.Add(new UserEmail { Email = "temporal@demo.test", IsPrimary = true });
        user.UserRoles.Add(new UserRole { Role = new Role { Code = SecurityCatalog.InternalUserRole, Name = "Usuario interno" } });
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static ClaimsPrincipal Principal(AppUser user, bool forcedChange = false) =>
        new(new ClaimsIdentity(forcedChange
            ? [new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new Claim(AuthService.PasswordChangeClaim, "1")]
            : [new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString())], "test"));

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"seed-tests-{Guid.NewGuid():N}").Options);

    private sealed class NoEmail : IEmailSender
    {
        public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken, bool isHtml = false) => Task.CompletedTask;
    }
}
