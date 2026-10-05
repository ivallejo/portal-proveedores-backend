using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Admin;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Entities;
using WebProveedores.Infrastructure.Persistence;
using WebProveedores.Application;

namespace WebProveedores.Tests;

public sealed class AdminUserServiceTests
{
    [Fact]
    public async Task Create_assigns_roles_area_and_companies_with_a_temporary_password()
    {
        await using var fixture = await Fixture.CreateAsync();

        var created = await fixture.Service.CreateAsync(new CreateUserRequest
        {
            Username = "ana.rios",
            Email = "Ana.Rios@Ejemplo.test",
            Name = "Ana Ríos",
            Password = "Temporal_1",
            Roles = [SecurityCatalog.AreaApproverRole, SecurityCatalog.InternalUserRole],
            AreaId = fixture.Area.Id,
            CompanyCodes = ["1002"],
        }, CancellationToken.None);

        Assert.Equal("ana.rios@ejemplo.test", created.Email);
        Assert.Equal([SecurityCatalog.InternalUserRole, SecurityCatalog.AreaApproverRole], created.Roles);
        Assert.Equal("Finanzas", created.AreaName);
        Assert.Equal(["1002"], created.CompanyCodes);
        Assert.True(created.MustChangePassword);
        var user = await fixture.Db.Users.SingleAsync(item => item.Username == "ana.rios");
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<AppUser>().VerifyHashedPassword(user, user.PasswordHash, "Temporal_1"));
    }

    [Theory]
    [InlineData(SecurityCatalog.AreaApproverRole, null, "1001", false, "necesita un área")]
    [InlineData(SecurityCatalog.ProviderRole, null, "1001", true, "RUC de 11 dígitos")]
    [InlineData(SecurityCatalog.InternalUserRole, null, null, true, "al menos una sociedad")]
    [InlineData(SecurityCatalog.InternalUserRole, "débil", "1001", true, "mínimo 8 caracteres")]
    [InlineData("NO_EXISTE", null, "1001", true, "Rol no válido")]
    public async Task Create_enforces_the_access_rules(string role, string? password, string? company, bool withArea, string expected)
    {
        await using var fixture = await Fixture.CreateAsync();

        var exception = await Assert.ThrowsAsync<ValidationException>(() => fixture.Service.CreateAsync(new CreateUserRequest
        {
            Username = "nuevo",
            Email = "nuevo@ejemplo.test",
            Name = "Nuevo",
            Password = password ?? "Temporal_1",
            Roles = [role],
            AreaId = withArea ? fixture.Area.Id : null,
            CompanyCodes = company is null ? [] : [company],
        }, CancellationToken.None));

        Assert.Contains(expected, exception.Message);
        Assert.False(await fixture.Db.Users.AnyAsync(user => user.Username == "nuevo"));
    }

    [Fact]
    public async Task Create_rejects_duplicates()
    {
        await using var fixture = await Fixture.CreateAsync();

        await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.CreateAsync(new CreateUserRequest
        {
            Username = "admin.uno",
            Email = "otro@ejemplo.test",
            Name = "Duplicado",
            Password = "Temporal_1",
            Roles = [SecurityCatalog.InternalUserRole],
            CompanyCodes = ["1001"],
        }, CancellationToken.None));
    }

    [Fact]
    public async Task Update_replaces_access_and_keeps_at_least_one_active_administrator()
    {
        await using var fixture = await Fixture.CreateAsync();
        var admin = fixture.Admin.Id;
        var other = fixture.OtherAdmin.Id;

        await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.UpdateAsync(admin, admin, Update("admin1@ejemplo.test", SecurityCatalog.InternalUserRole), CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.SetStatusAsync(admin, admin, new UpdateUserStatusRequest(false), CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.UpdateAsync(admin, other, Update("admin1@ejemplo.test", SecurityCatalog.AdministratorRole), CancellationToken.None));

        // Quitar el rol al otro administrador es válido mientras quede uno activo.
        var updated = await fixture.Service.UpdateAsync(admin, other, Update("dos@ejemplo.test", SecurityCatalog.AccountsPayableRole), CancellationToken.None);
        Assert.Equal([SecurityCatalog.AccountsPayableRole], updated!.Roles);
        Assert.Equal(["1001"], updated.CompanyCodes);
        Assert.Equal("dos@ejemplo.test", updated.Email);

        // Ahora es el único: nadie puede desactivarlo.
        await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.SetStatusAsync(other, admin, new UpdateUserStatusRequest(false), CancellationToken.None));
    }

    [Fact]
    public async Task Unlock_clears_the_lockout_and_search_filters_and_paginates()
    {
        await using var fixture = await Fixture.CreateAsync();
        var locked = await fixture.Db.Users.SingleAsync(user => user.Id == fixture.OtherAdmin.Id);
        for (var attempt = 0; attempt < 5; attempt++) locked.RecordFailedLogin(5, 10, DateTime.UtcNow);
        await fixture.Db.SaveChangesAsync();

        Assert.True((await fixture.Service.SearchAsync("admin2", 1, 10, CancellationToken.None)).Items.Single().IsLocked);
        var unlocked = await fixture.Service.UnlockAsync(locked.Id, CancellationToken.None);
        Assert.False(unlocked!.IsLocked);
        Assert.Equal(0, (await fixture.Db.Users.SingleAsync(user => user.Id == locked.Id)).FailedLoginCount);

        var page = await fixture.Service.SearchAsync(null, 1, 1, CancellationToken.None);
        Assert.Equal(2, page.Total);
        Assert.Single(page.Items);
    }

    private static UpdateUserRequest Update(string email, string role) => new()
    {
        Email = email,
        Name = "Editado",
        Roles = [role],
        CompanyCodes = ["1001"],
    };

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(AppDbContext db) => Db = db;

        public AppDbContext Db { get; }
        public AdminUserService Service { get; private set; } = null!;
        public Area Area { get; private set; } = null!;
        public AppUser Admin { get; private set; } = null!;
        public AppUser OtherAdmin { get; private set; } = null!;

        public static async Task<Fixture> CreateAsync()
        {
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"admin-{Guid.NewGuid():N}").Options);
            var fixture = new Fixture(db);
            var roles = SecurityCatalog.Roles.ToDictionary(role => role.Key, role => new Role { Code = role.Key, Name = role.Value });
            db.Roles.AddRange(roles.Values);
            fixture.Area = new Area { Code = "FINANZAS", Name = "Finanzas" };
            db.Areas.Add(fixture.Area);
            db.Companies.AddRange(new Company { Code = "1001", Name = "Naviera Transoceánica" }, new Company { Code = "1002", Name = "Ultratag" });

            AppUser Admin(string username, string email)
            {
                var user = AppUser.Create(username, username, null, email, "x", DateTime.UtcNow);
                user.SetRoles([roles[SecurityCatalog.AdministratorRole]]);
                db.Users.Add(user);
                return user;
            }

            fixture.Admin = Admin("admin.uno", "admin1@ejemplo.test");
            fixture.OtherAdmin = Admin("admin2", "admin2@ejemplo.test");
            await db.SaveChangesAsync();
            fixture.Service = TestServices.Admin(db);
            return fixture;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
