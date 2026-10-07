using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Auth;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Application.Ports.Outbound.Notifications;
using WebProveedores.Application.Profile;
using WebProveedores.Domain.Access;
using WebProveedores.Domain.Common;
using WebProveedores.Domain.Identity;
using WebProveedores.Infrastructure.Persistence;

namespace WebProveedores.Tests;

public sealed class ProfileServiceTests
{
    [Fact]
    public async Task Provider_edits_business_name_and_internal_user_edits_names()
    {
        await using var db = CreateContext();
        var provider = AddUser(db, "20100003199", "proveedor@ejemplo.test", ruc: "20100003199");
        var internalUser = AddUser(db, "45678123", "maria@ejemplo.test");
        await db.SaveChangesAsync();
        var service = Service(db, new FakeEmailSender());

        var renamed = await service.UpdateAsync(provider.Id, new UpdateProfileRequest { BusinessName = "  Andes Suministros S.A.C. " }, CancellationToken.None);
        Assert.Equal(("Andes Suministros S.A.C.", true), (renamed.BusinessName, renamed.IsProvider));
        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateAsync(provider.Id, new UpdateProfileRequest { BusinessName = "An" }, CancellationToken.None));

        var named = await service.UpdateAsync(internalUser.Id, new UpdateProfileRequest { FirstName = "María", LastName = "Torres Vega" }, CancellationToken.None);
        Assert.Equal(("María", "Torres Vega", "María Torres Vega"), (named.FirstName, named.LastName, named.DisplayName));
        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateAsync(internalUser.Id, new UpdateProfileRequest { FirstName = "María", LastName = "Torres V3ga" }, CancellationToken.None));
        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateAsync(internalUser.Id, new UpdateProfileRequest { FirstName = "", LastName = "Torres" }, CancellationToken.None));
    }

    [Fact]
    public async Task New_email_must_be_verified_before_login_or_becoming_primary()
    {
        await using var db = CreateContext();
        var user = AddUser(db, "20100003199", "facturacion@andes.test", ruc: "20100003199");
        AddUser(db, "otro", "otro@ejemplo.test");
        await db.SaveChangesAsync();
        var email = new FakeEmailSender();
        var service = Service(db, email);

        await Assert.ThrowsAsync<ValidationException>(() => service.AddEmailAsync(user.Id, new AddEmailRequest { Email = "sin-arroba", Type = "work" }, CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() => service.AddEmailAsync(user.Id, new AddEmailRequest { Email = "OTRO@ejemplo.test", Type = "work" }, CancellationToken.None));

        var profile = await service.AddEmailAsync(user.Id, new AddEmailRequest { Email = " Cobranzas@Andes.test ", Type = "billing" }, CancellationToken.None);
        var added = profile.Emails.Single(item => !item.IsPrimary);
        Assert.Equal(("cobranzas@andes.test", "billing", false), (added.Email, added.Type, added.IsVerified));
        Assert.Equal("cobranzas@andes.test", email.Recipient);
        var token = Regex.Match(email.Body!, "emailToken=([A-Za-z0-9_-]+)").Groups[1].Value;

        // Sin verificar no sirve para ingresar ni para ser principal.
        Assert.Null(await TestServices.Login(db).LoginAsync(new LoginRequest { Identifier = "cobranzas@andes.test", Password = "Password1" }, CancellationToken.None));
        await Assert.ThrowsAsync<DomainRuleException>(() => service.MakePrimaryAsync(user.Id, added.Id, CancellationToken.None));

        Assert.Null(await service.VerifyEmailAsync("token-falso", CancellationToken.None));
        Assert.Equal("cobranzas@andes.test", await service.VerifyEmailAsync(token, CancellationToken.None));
        Assert.Null(await service.VerifyEmailAsync(token, CancellationToken.None));
        Assert.NotNull(await TestServices.Login(db).LoginAsync(new LoginRequest { Identifier = "cobranzas@andes.test", Password = "Password1" }, CancellationToken.None));

        var switched = await service.MakePrimaryAsync(user.Id, added.Id, CancellationToken.None);
        Assert.Equal("cobranzas@andes.test", switched.Emails.Single(item => item.IsPrimary).Email);
        await Assert.ThrowsAsync<DomainRuleException>(() => service.RemoveEmailAsync(user.Id, added.Id, CancellationToken.None));

        var old = switched.Emails.Single(item => !item.IsPrimary);
        var removed = await service.RemoveEmailAsync(user.Id, old.Id, CancellationToken.None);
        Assert.Single(removed.Emails);
        await Assert.ThrowsAsync<NotFoundException>(() => service.ResendVerificationAsync(user.Id, Guid.NewGuid(), CancellationToken.None));
    }

    private static AppUser AddUser(AppDbContext db, string username, string email, string? ruc = null)
    {
        var user = AppUser.Create(username, username, ruc, email, TestServices.Hasher.Hash("Password1"), DateTime.UtcNow);
        user.SetRoles([new Role { Code = ruc is null ? SecurityCatalog.InternalUserRole : SecurityCatalog.ProviderRole, Name = "Rol" }]);
        db.Users.Add(user);
        return user;
    }

    private static ProfileService Service(AppDbContext db, IEmailSender email) =>
        new(new EfUserRepository(db), new EfUnitOfWork(db), new EmailVerifications(email, TestServices.Portal, TimeProvider.System), TimeProvider.System);

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"profile-{Guid.NewGuid():N}").Options);

    private sealed class FakeEmailSender : IEmailSender
    {
        public string? Recipient { get; private set; }
        public string? Body { get; private set; }

        public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken, bool isHtml = false, IReadOnlyList<string>? copyTo = null)
        {
            Recipient = recipient;
            Body = body;
            return Task.CompletedTask;
        }
    }
}
