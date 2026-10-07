using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using WebProveedores.Application.Abstractions.Auth;
using WebProveedores.Application.Auth;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Domain.Access;
using WebProveedores.Domain.Identity;
using WebProveedores.Infrastructure.Persistence;
using WebProveedores.Infrastructure.Providers;

namespace WebProveedores.Tests;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_returns_token_for_valid_credentials()
    {
        await using var db = CreateContext();
        var user = CreateUser("20523682785", "proveedor", "Proveedor Andino SAC", "proveedor@demo.test");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = TestServices.Login(db);

        var response = await service.LoginAsync(new LoginRequest { Identifier = "20523682785", Password = "Password1" }, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("proveedor", response.User.Username);
        Assert.Contains("Proveedor", response.User.Roles);
    }

    [Fact]
    public async Task LoginAsync_locks_the_account_after_repeated_failures_and_recovers_after_the_lockout()
    {
        await using var db = CreateContext();
        var user = CreateUser("20523682790", "lock-user", "Proveedor Bloqueo", "lock@demo.test");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var service = TestServices.Login(db, clock);
        var wrong = new LoginRequest { Identifier = "lock-user", Password = "incorrecta" };

        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Null(await service.LoginAsync(wrong, CancellationToken.None));

        // Bloqueada: ni siquiera la contraseña correcta entra.
        var locked = await Assert.ThrowsAsync<AccountLockedException>(() =>
            service.LoginAsync(new LoginRequest { Identifier = "lock-user", Password = "Password1" }, CancellationToken.None));
        Assert.True(locked.RetryAfter > TimeSpan.FromMinutes(14));

        clock.Advance(TimeSpan.FromMinutes(16));
        var response = await service.LoginAsync(new LoginRequest { Identifier = "lock-user", Password = "Password1" }, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockoutUntilUtc);
    }

    [Fact]
    public async Task LoginAsync_resets_the_failure_counter_after_a_successful_login()
    {
        await using var db = CreateContext();
        var user = CreateUser("20523682791", "counter-user", "Proveedor Contador", "counter@demo.test");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = TestServices.Login(db);

        for (var attempt = 0; attempt < 3; attempt++)
            await service.LoginAsync(new LoginRequest { Identifier = "counter-user", Password = "mala" }, CancellationToken.None);
        Assert.Equal(3, user.FailedLoginCount);
        await service.LoginAsync(new LoginRequest { Identifier = "counter-user", Password = "Password1" }, CancellationToken.None);

        Assert.Equal(0, user.FailedLoginCount);
    }

    [Fact]
    public async Task LoginAsync_returns_null_for_unknown_users_without_locking_anything()
    {
        await using var db = CreateContext();
        var service = TestServices.Login(db);

        for (var attempt = 0; attempt < 7; attempt++)
            Assert.Null(await service.LoginAsync(new LoginRequest { Identifier = "no-existe", Password = "x" }, CancellationToken.None));
    }

    [Fact]
    public async Task LoginAsync_rejects_inactive_user()
    {
        await using var db = CreateContext();
        var user = CreateUser("20523682786", "inactive", "Proveedor Inactivo", "inactive@demo.test");
        user.SetActive(false, DateTime.UtcNow);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = TestServices.Login(db);

        var response = await service.LoginAsync(new LoginRequest { Identifier = "inactive", Password = "Password1" }, CancellationToken.None);

        Assert.Null(response);
    }

    [Fact]
    public async Task ConfirmPasswordResetAsync_rejects_activation_token_on_password_reset_endpoint()
    {
        await using var db = CreateContext();
        var user = CreateUser("20523682787", "activation-user", "Proveedor Activación", "activation@demo.test");
        db.Users.Add(user);
        const string token = "activation-token";
        db.PasswordResetTokens.Add(CreateToken(user, token, PasswordTokenPurpose.Activation));
        await db.SaveChangesAsync();
        var service = TestServices.Passwords(db, new FakeEmailSender());

        var confirmed = await service.ConfirmPasswordResetAsync(
            new PasswordResetConfirmRequest { Ruc = user.Ruc!, Token = token, NewPassword = "NewPassword1" },
            PasswordTokenPurpose.PasswordReset,
            CancellationToken.None);

        Assert.False(confirmed);
        Assert.Null(db.PasswordResetTokens.Single().UsedAtUtc);
    }

    [Fact]
    public async Task ConfirmPasswordResetAsync_consumes_matching_token_once()
    {
        await using var db = CreateContext();
        var user = CreateUser("20523682788", "reset-user", "Proveedor Recuperación", "reset@demo.test");
        db.Users.Add(user);
        const string token = "reset-token";
        db.PasswordResetTokens.Add(CreateToken(user, token, PasswordTokenPurpose.PasswordReset));
        await db.SaveChangesAsync();
        var service = TestServices.Passwords(db, new FakeEmailSender());
        var request = new PasswordResetConfirmRequest { Ruc = user.Ruc!, Token = token, NewPassword = "NewPassword1" };

        var firstConfirmation = await service.ConfirmPasswordResetAsync(request, PasswordTokenPurpose.PasswordReset, CancellationToken.None);
        var secondConfirmation = await service.ConfirmPasswordResetAsync(request, PasswordTokenPurpose.PasswordReset, CancellationToken.None);

        Assert.True(firstConfirmation);
        Assert.False(secondConfirmation);
        Assert.NotNull(db.PasswordResetTokens.Single().UsedAtUtc);
    }

    [Fact]
    public async Task ConfirmPasswordResetAsync_applies_the_password_policy_and_keeps_the_token()
    {
        await using var db = CreateContext();
        var user = CreateUser("20523682789", "activation-user", "Proveedor Activación", "activation@demo.test");
        db.Users.Add(user);
        db.PasswordResetTokens.Add(CreateToken(user, "activation-token", PasswordTokenPurpose.Activation));
        await db.SaveChangesAsync();
        var service = TestServices.Passwords(db, new FakeEmailSender());

        await Assert.ThrowsAsync<ValidationException>(() => service.ConfirmPasswordResetAsync(
            new PasswordResetConfirmRequest { Ruc = user.Ruc!, Token = "activation-token", NewPassword = "abc123" }, PasswordTokenPurpose.Activation, CancellationToken.None));

        Assert.Null(db.PasswordResetTokens.Single().UsedAtUtc);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_creates_password_reset_token_and_sends_email()
    {
        await using var db = CreateContext();
        var user = CreateUser("20523682789", "reset-request", "Proveedor Solicitud", "request@demo.test");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var emailSender = new FakeEmailSender();
        var service = TestServices.Passwords(db, emailSender);

        var response = await service.RequestPasswordResetAsync(new PasswordResetRequest { Ruc = user.Ruc! }, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("req*****demo.test", response.MaskedEmail);
        Assert.Equal(PasswordTokenPurpose.PasswordReset, db.PasswordResetTokens.Single().Purpose);
        Assert.Equal("request@demo.test", emailSender.Recipient);
        Assert.Contains("resetToken=", emailSender.Body);
    }

    [Fact]
    public async Task ValidateRucAsync_rejects_existing_user_before_calling_sap()
    {
        await using var db = CreateContext();
        var user = CreateUser("20523682780", "registered", "Proveedor Registrado", "registered@demo.test", activated: true);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var sap = new SapProviderClient(new HttpClient(new ThrowingHandler()), new SapSettings("http://sap.invalid", "200", "test-token"));
        var service = TestServices.Registration(db, new FakeEmailSender(), sap);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => service.ValidateRucAsync(user.Ruc!, CancellationToken.None));

        Assert.Equal("El usuario ya se encuentra registrado.", exception.Message);
    }

    [Fact]
    public async Task ValidateRucAsync_allows_retrying_a_registration_that_was_never_activated()
    {
        await using var db = CreateContext();
        var pending = CreateUser("20100003199", "20100003199", "Proveedor sin activar", "pendiente@demo.test");
        db.Users.Add(pending);
        await db.SaveChangesAsync();
        var sap = new SapProviderClient(new HttpClient(new UnreachableHandler()), new SapSettings("http://sap.invalid", "200", "test-token"));
        var service = TestServices.Registration(db, new FakeEmailSender(), sap);

        // No lo rechaza como «ya registrado»: sigue a la consulta en SAP (aquí caído).
        await Assert.ThrowsAsync<ServiceUnavailableException>(() => service.ValidateRucAsync("20100003199", CancellationToken.None));
    }

    [Fact]
    public async Task ValidateRucAsync_reports_sap_outages_as_service_unavailable()
    {
        await using var db = CreateContext();
        var sap = new SapProviderClient(new HttpClient(new UnreachableHandler()), new SapSettings("http://sap.invalid", "200", "test-token"));
        var service = TestServices.Registration(db, new FakeEmailSender(), sap);

        var exception = await Assert.ThrowsAsync<ServiceUnavailableException>(() => service.ValidateRucAsync("20100003199", CancellationToken.None));

        Assert.Contains("SAP", exception.Message);
    }

    [Fact]
    public async Task ValidateRucAsync_explains_when_the_provider_has_no_email_in_sap()
    {
        await using var db = CreateContext();
        var sap = new SapProviderClient(
            new HttpClient(new JsonHandler("[{\"stcd1\": \"\", \"name1\": \"TELEFONICA DEL PERU S.A.A.\", \"name2\": \"\", \"adrnr\": \"0000051096\", \"correo\": \"\"}]")),
            new SapSettings("http://sap.invalid", "200", "test-token"));
        var service = TestServices.Registration(db, new FakeEmailSender(), sap);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.ValidateRucAsync("20100017491", CancellationToken.None));

        Assert.Contains("no tiene un correo de contacto", exception.Message);
    }

    private sealed class JsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") });
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"auth-tests-{Guid.NewGuid():N}")
        .Options);

    private static AppUser CreateUser(string ruc, string username, string company, string email, bool activated = false)
    {
        var user = AppUser.Create(username, company, ruc, email, TestServices.Hasher.Hash("Password1"), DateTime.UtcNow, activated: activated);
        user.SetRoles([new Role { Code = SecurityCatalog.ProviderRole, Name = "Proveedor" }]);
        return user;
    }

    private static PasswordResetToken CreateToken(AppUser user, string token, PasswordTokenPurpose purpose) => new()
    {
        UserId = user.Id,
        User = user,
        TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
        Purpose = purpose,
        ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
    };

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

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("SAP no debió ser consultado.");
    }

    private sealed class UnreachableHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("nodename nor servname provided, or not known");
    }
}
