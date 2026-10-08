using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Application.Contracts.Access.Commands;
using WebProveedores.Application.Ports.Inbound.Access;
using WebProveedores.Application.UseCases.Access;
using WebProveedores.Domain.Access;
using WebProveedores.Domain.Identity;
using WebProveedores.Infrastructure.Persistence;
using WebProveedores.Infrastructure.Persistence.Repositories;

namespace WebProveedores.Tests;

public sealed class AccessAdminServiceTests
{
    [Fact]
    public async Task A_new_role_gets_its_menus_and_the_parent_of_each_submenu()
    {
        await using var fixture = await Fixture.CreateAsync();
        var users = fixture.Menu(MenuCatalog.SettingsUsers);

        var role = await fixture.Service.CreateRoleAsync(new SaveRoleCommand
        {
            Name = "Tesorería",
            Description = "Consulta pagos.",
            MenuIds = [fixture.Menu(MenuCatalog.PaymentOrders).Id, users.Id, fixture.Menu(MenuCatalog.Settings).Id],
        }, CancellationToken.None);

        Assert.Equal(("TESORERIA", false), (role.Code, role.IsSystem));
        Assert.Equal(
            new[] { MenuCatalog.PaymentOrders, MenuCatalog.SettingsUsers, MenuCatalog.Settings }.Order(),
            role.MenuIds.Select(id => fixture.Db.MenuOptions.Single(menu => menu.Id == id).Code).Order());
        await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.CreateRoleAsync(new SaveRoleCommand { Name = "tesorería", MenuIds = [users.Id] }, CancellationToken.None));
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Service.CreateRoleAsync(new SaveRoleCommand { Name = "Vacío" }, CancellationToken.None));
    }

    [Fact]
    public async Task The_administrator_keeps_roles_and_menus_administration()
    {
        await using var fixture = await Fixture.CreateAsync();
        var admin = fixture.Roles[SecurityCatalog.AdministratorRole];

        var error = await Assert.ThrowsAsync<ValidationException>(() => fixture.Service.UpdateRoleAsync(admin.Id,
            new SaveRoleCommand { Name = admin.Name, MenuIds = [fixture.Menu(MenuCatalog.Home).Id] }, CancellationToken.None));
        Assert.Contains("Roles y permisos", error.Message);
        await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.SetRoleStatusAsync(admin.Id, false, CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.SetMenuStatusAsync(fixture.Menu(MenuCatalog.SettingsRoles).Id, false, CancellationToken.None));
    }

    [Fact]
    public async Task Menus_have_two_levels_and_system_options_keep_their_route()
    {
        await using var fixture = await Fixture.CreateAsync();
        var settings = fixture.Menu(MenuCatalog.Settings);

        var reports = await fixture.Service.CreateMenuAsync(new SaveMenuCommand { Name = "Reportes", Route = "/configuracion/reportes", Icon = "chart-bar", Order = 6, ParentId = settings.Id }, CancellationToken.None);
        Assert.Equal("MENU_REPORTES", reports.Code);
        Assert.Equal(1, reports.RoleCount);

        await Assert.ThrowsAsync<ValidationException>(() => fixture.Service.CreateMenuAsync(
            new SaveMenuCommand { Name = "Nieto", Route = "/x", Icon = "home", ParentId = reports.Id }, CancellationToken.None));
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Service.CreateMenuAsync(
            new SaveMenuCommand { Name = "Sin ruta", Icon = "home", ParentId = settings.Id }, CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.CreateMenuAsync(
            new SaveMenuCommand { Name = "Otra", Route = "/orden-pago", Icon = "home" }, CancellationToken.None));

        var home = fixture.Menu(MenuCatalog.Home);
        var renamed = await fixture.Service.UpdateMenuAsync(home.Id, new SaveMenuCommand { Name = "Principal", Route = "/inicio", Icon = "home", Order = 1 }, CancellationToken.None);
        Assert.Equal("Principal", renamed.Name);
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Service.UpdateMenuAsync(home.Id,
            new SaveMenuCommand { Name = "Principal", Route = "/otra", Icon = "home", Order = 1 }, CancellationToken.None));
    }

    [Fact]
    public async Task Navigation_shows_only_the_active_options_of_the_role()
    {
        await using var fixture = await Fixture.CreateAsync();
        var approver = AppUser.Create("aprobador", "Aprobador", null, "aprobador@ejemplo.test", "x", DateTime.UtcNow);
        approver.SetRoles([fixture.Roles[SecurityCatalog.AreaApproverRole]]);
        var admin = AppUser.Create("admin", "Admin", null, "admin@ejemplo.test", "x", DateTime.UtcNow);
        admin.SetRoles([fixture.Roles[SecurityCatalog.AdministratorRole]]);
        fixture.Db.Users.AddRange(approver, admin);
        await fixture.Db.SaveChangesAsync();
        var navigation = new NavigationService(new EfAccessRepository(fixture.Db), new EfPermissionReader(fixture.Db));

        Assert.Equal([MenuCatalog.Home, MenuCatalog.Documents], (await navigation.MenuForAsync(approver.Id, CancellationToken.None)).Select(item => item.Code));

        await fixture.Service.SetMenuStatusAsync(fixture.Menu(MenuCatalog.SettingsUsers).Id, false, CancellationToken.None);
        var settings = (await navigation.MenuForAsync(admin.Id, CancellationToken.None)).Single(item => item.Code == MenuCatalog.Settings);
        Assert.DoesNotContain(settings.Children, item => item.Code == MenuCatalog.SettingsUsers);
        Assert.Equal("/configuracion/sociedades", settings.Children[0].Route);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(AppDbContext db) => Db = db;

        public AppDbContext Db { get; }
        public Dictionary<string, Role> Roles { get; private set; } = [];
        public IAccessAdminService Service { get; private set; } = null!;

        public MenuOption Menu(string code) => Db.MenuOptions.Single(menu => menu.Code == code);

        public static async Task<Fixture> CreateAsync()
        {
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"access-{Guid.NewGuid():N}").Options);
            var fixture = new Fixture(db);
            fixture.Roles = SecurityCatalog.Roles.ToDictionary(role => role.Key, role => new Role { Code = role.Key, Name = role.Value });
            db.Roles.AddRange(fixture.Roles.Values);
            TestMenus.Seed(db, fixture.Roles);
            await db.SaveChangesAsync();
            fixture.Service = new AccessAdminService(new EfAccessRepository(db), new EfUnitOfWork(db), TimeProvider.System);
            return fixture;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
