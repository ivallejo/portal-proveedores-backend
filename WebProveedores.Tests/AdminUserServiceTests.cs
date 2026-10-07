using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Abstractions.Auth;
using WebProveedores.Application.Admin;
using WebProveedores.Application.Auth;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Domain.Access;
using WebProveedores.Domain.Common;
using WebProveedores.Domain.Identity;
using WebProveedores.Domain.Organization;
using WebProveedores.Infrastructure.Persistence;

namespace WebProveedores.Tests;

public sealed class AdminUserServiceTests
{
    [Fact]
    public async Task New_internal_user_activates_with_the_link_sent_to_the_primary_email()
    {
        await using var fixture = await Fixture.CreateAsync();

        var created = await fixture.Service.CreateAsync(Internal("45678123", fixture.Finanzas.Id,
            Mail("Maria.Torres@Ejemplo.test", primary: true), Mail("mtorres@personal.test", type: "personal")), CancellationToken.None);

        Assert.Equal(("45678123", "DNI", "María Torres Vega"), (created.Document, created.DocumentType, created.DisplayName));
        Assert.Equal((SecurityCatalog.AreaApproverRole, "active", false), (created.Role, created.Status, created.IsActivated));
        Assert.All(created.Emails, email => Assert.False(email.IsVerified));
        var activation = fixture.Email.Sent.Single(mail => mail.Recipient == "maria.torres@ejemplo.test");
        Assert.Matches("user=45678123(&amp;|&)activationToken=", activation.Body);
        Assert.Contains(fixture.Email.Sent, mail => mail.Recipient == "mtorres@personal.test" && mail.Body.Contains("emailToken="));

        var token = Regex.Match(activation.Body, "activationToken=([A-Za-z0-9_-]+)").Groups[1].Value;
        var passwords = TestServices.Passwords(fixture.Db, fixture.Email);
        Assert.False(await passwords.ConfirmPasswordResetAsync(Confirm(token, user: "99999999"), PasswordTokenPurpose.Activation, CancellationToken.None));
        Assert.True(await passwords.ConfirmPasswordResetAsync(Confirm(token, user: "45678123"), PasswordTokenPurpose.Activation, CancellationToken.None));

        var detail = await fixture.Service.GetAsync(created.Id, CancellationToken.None);
        Assert.True(detail!.IsActivated);
        Assert.True(detail.Emails.Single(email => email.IsPrimary).IsVerified);
        Assert.NotNull(await TestServices.Login(fixture.Db).LoginAsync(new LoginRequest { Identifier = "45678123", Password = "Nueva_Clave1" }, CancellationToken.None));
    }

    [Theory]
    [InlineData("PROVIDER", "30123456789", "empezar con 10 o 20")]
    [InlineData("AREA_APPROVER", "1234", "8 dígitos")]
    [InlineData("AREA_APPROVER", "45678123", "Selecciona el área", false)]
    [InlineData("AREA_APPROVER", "45678123", "al menos una sociedad", true, "")]
    [InlineData("AREA_APPROVER", "45678123", "asigna también esa sociedad", true, "1002")]
    [InlineData("AREA_APPROVER", "45678123", "al menos un correo", true, "1001", false)]
    public async Task Create_enforces_the_rules(string role, string document, string expected, bool withArea = true, string company = "1001", bool withEmail = true)
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = new SaveUserRequest
        {
            Role = role,
            Document = document,
            BusinessName = "Proveedor S.A.C.",
            FirstName = "María",
            LastName = "Torres",
            AreaId = withArea ? fixture.Finanzas.Id : null,
            CompanyCodes = company.Length == 0 ? [] : [company],
            Emails = withEmail ? [Mail("nuevo@ejemplo.test", primary: true)] : [],
        };

        var error = await Assert.ThrowsAsync<ValidationException>(() => fixture.Service.CreateAsync(request, CancellationToken.None));

        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public async Task Create_rejects_repeated_documents_and_emails()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.CreateAsync(Internal("45678123", fixture.Finanzas.Id, Mail("maria@ejemplo.test", primary: true)), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.CreateAsync(
            Internal("45678123", fixture.Finanzas.Id, Mail("otro@ejemplo.test", primary: true)), CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.CreateAsync(
            Internal("41239876", fixture.Finanzas.Id, Mail("admin1@ejemplo.test", primary: true)), CancellationToken.None));
    }

    [Fact]
    public async Task Update_keeps_an_active_administrator_and_the_account_type()
    {
        await using var fixture = await Fixture.CreateAsync();
        var adminEmail = fixture.Admin.Emails.Single();

        var selfDemotion = Edit(SecurityCatalog.InternalUserRole, fixture.Finanzas.Id, Existing(adminEmail));
        await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.UpdateAsync(fixture.Admin.Id, fixture.Admin.Id, selfDemotion, CancellationToken.None));
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Service.UpdateAsync(fixture.Admin.Id, fixture.Admin.Id,
            Edit(SecurityCatalog.ProviderRole, null, Existing(adminEmail)), CancellationToken.None));

        // Otro administrador sí puede quitarle el rol: queda uno activo.
        var demoted = await fixture.Service.UpdateAsync(fixture.OtherAdmin.Id, fixture.Admin.Id, selfDemotion, CancellationToken.None);
        Assert.Equal(SecurityCatalog.InternalUserRole, demoted!.Role);
        var lastAdmin = fixture.OtherAdmin.Emails.Single();
        await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.UpdateAsync(fixture.Admin.Id, fixture.OtherAdmin.Id,
            Edit(SecurityCatalog.InternalUserRole, fixture.Finanzas.Id, Existing(lastAdmin)), CancellationToken.None));
    }

    [Fact]
    public async Task Update_manages_emails_status_and_the_forced_password_change()
    {
        await using var fixture = await Fixture.CreateAsync();
        var original = fixture.OtherAdmin.Emails.Single();
        var request = Edit(SecurityCatalog.AdministratorRole, null, Existing(original, primary: false), Mail("nuevo@ejemplo.test", primary: true));

        // El nuevo correo aún no está verificado: no puede ser principal de una cuenta activa.
        await Assert.ThrowsAsync<DomainRuleException>(() => fixture.Service.UpdateAsync(fixture.Admin.Id, fixture.OtherAdmin.Id, request, CancellationToken.None));

        var updated = await fixture.Service.UpdateAsync(fixture.Admin.Id, fixture.OtherAdmin.Id,
            Edit(SecurityCatalog.AdministratorRole, null, Existing(original), Mail("nuevo@ejemplo.test", type: "billing")) with { MustChangePassword = true, Status = "inactive" },
            CancellationToken.None);

        Assert.Equal(2, updated!.Emails.Count);
        Assert.Equal(("billing", false), (updated.Emails[1].Type, updated.Emails[1].IsVerified));
        Assert.Equal(("inactive", true), (updated.Status, updated.MustChangePassword));
        Assert.Contains(fixture.Email.Sent, mail => mail.Recipient == "nuevo@ejemplo.test");

        var removed = await fixture.Service.UpdateAsync(fixture.Admin.Id, fixture.OtherAdmin.Id,
            Edit(SecurityCatalog.AdministratorRole, null, Existing(original)), CancellationToken.None);
        Assert.Single(removed!.Emails);
    }

    [Fact]
    public async Task Search_filters_by_role_and_status_and_activating_unlocks()
    {
        await using var fixture = await Fixture.CreateAsync();
        var locked = await fixture.Db.Users.SingleAsync(user => user.Id == fixture.OtherAdmin.Id);
        for (var attempt = 0; attempt < 5; attempt++) locked.RecordFailedLogin(5, 10, DateTime.UtcNow);
        await fixture.Db.SaveChangesAsync();

        var page = await fixture.Service.SearchAsync(null, null, "locked", 1, 10, CancellationToken.None);
        Assert.Equal("admin2", page.Items.Single().Document);
        Assert.Equal(new AdminUserCounts(2, 1, 1), page.Counts);
        Assert.Equal(2, (await fixture.Service.SearchAsync(null, SecurityCatalog.AdministratorRole, null, 1, 10, CancellationToken.None)).Total);
        Assert.Empty((await fixture.Service.SearchAsync(null, SecurityCatalog.ProviderRole, null, 1, 10, CancellationToken.None)).Items);

        var unlocked = await fixture.Service.SetStatusAsync(fixture.Admin.Id, locked.Id, new UpdateUserStatusRequest(true), CancellationToken.None);
        Assert.Equal("active", unlocked!.Status);
        await Assert.ThrowsAsync<ConflictException>(() => fixture.Service.SetStatusAsync(fixture.Admin.Id, fixture.Admin.Id, new UpdateUserStatusRequest(false), CancellationToken.None));
    }

    [Fact]
    public async Task A_new_recovery_link_replaces_the_previous_one()
    {
        await using var fixture = await Fixture.CreateAsync();

        var first = await fixture.Service.SendPasswordLinkAsync(fixture.OtherAdmin.Id, CancellationToken.None);
        await fixture.Service.SendPasswordLinkAsync(fixture.OtherAdmin.Id, CancellationToken.None);

        Assert.Equal(("reset", "admin2@ejemplo.test"), (first!.Kind, first.Email));
        var links = await fixture.Service.PasswordLinksAsync(fixture.OtherAdmin.Id, CancellationToken.None);
        Assert.Equal(["valid", "replaced"], links!.Select(link => link.Status));
        var oldToken = Regex.Match(fixture.Email.Sent[0].Body, "resetToken=([A-Za-z0-9_-]+)").Groups[1].Value;
        Assert.False(await TestServices.Passwords(fixture.Db, fixture.Email)
            .ConfirmPasswordResetAsync(Confirm(oldToken, user: "admin2"), PasswordTokenPurpose.PasswordReset, CancellationToken.None));
    }

    private static UserEmailInput Mail(string email, bool primary = false, string type = "work") => new() { Email = email, IsPrimary = primary, Type = type };

    private static UserEmailInput Existing(UserEmail email, bool primary = true) => new() { Id = email.Id, Email = email.Email, IsPrimary = primary };

    private static SaveUserRequest Internal(string dni, Guid areaId, params UserEmailInput[] emails) => new()
    {
        Role = SecurityCatalog.AreaApproverRole,
        Document = dni,
        FirstName = "María",
        LastName = "Torres Vega",
        AreaId = areaId,
        CompanyCodes = ["1001"],
        Emails = emails,
    };

    private static SaveUserRequest Edit(string role, Guid? areaId, params UserEmailInput[] emails) => new()
    {
        Role = role,
        FirstName = "Nombre",
        LastName = "Editado",
        AreaId = areaId,
        CompanyCodes = ["1001"],
        Emails = emails,
    };

    private static PasswordResetConfirmRequest Confirm(string token, string user) => new() { Token = token, User = user, NewPassword = "Nueva_Clave1" };

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(AppDbContext db) => Db = db;

        public AppDbContext Db { get; }
        public CapturingEmailSender Email { get; } = new();
        public IAdminUserService Service { get; private set; } = null!;
        public Area Finanzas { get; private set; } = null!;
        public AppUser Admin { get; private set; } = null!;
        public AppUser OtherAdmin { get; private set; } = null!;

        public static async Task<Fixture> CreateAsync()
        {
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"admin-{Guid.NewGuid():N}").Options);
            var fixture = new Fixture(db);
            var roles = SecurityCatalog.Roles.ToDictionary(role => role.Key, role => new Role { Code = role.Key, Name = role.Value });
            db.Roles.AddRange(roles.Values);
            TestMenus.Seed(db, roles);
            var naviera = new Company { Code = "1001", Name = "Naviera Transoceánica" };
            db.Companies.AddRange(naviera, new Company { Code = "1002", Name = "Petrolera Transoceánica" });
            fixture.Finanzas = new Area { Code = "FINANZAS", Name = "Finanzas", Company = naviera, CompanyId = naviera.Id };
            db.Areas.Add(fixture.Finanzas);

            AppUser Admin(string username, string email)
            {
                var user = AppUser.Create(username, username, null, email, TestServices.Hasher.Hash("Password1"), DateTime.UtcNow);
                user.SetRoles([roles[SecurityCatalog.AdministratorRole]]);
                user.SetCompanies([naviera]);
                db.Users.Add(user);
                return user;
            }

            fixture.Admin = Admin("admin.uno", "admin1@ejemplo.test");
            fixture.OtherAdmin = Admin("admin2", "admin2@ejemplo.test");
            await db.SaveChangesAsync();
            fixture.Service = TestServices.Admin(db, fixture.Email);
            return fixture;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}

/// <summary>Guarda los correos enviados para revisar destinatarios y enlaces.</summary>
internal sealed class CapturingEmailSender : IEmailSender
{
    public List<(string Recipient, string Body)> Sent { get; } = [];

    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken, bool isHtml = false, IReadOnlyList<string>? copyTo = null)
    {
        Sent.Add((recipient, body));
        return Task.CompletedTask;
    }
}
