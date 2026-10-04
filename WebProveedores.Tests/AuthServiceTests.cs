using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WebProveedores.Application.Auth;
using WebProveedores.Application.Abstractions.Auth;
using WebProveedores.Domain.Entities;
using WebProveedores.Infrastructure.Auth;
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
        var service = CreateService(db);

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
        var service = CreateService(db);
        var wrong = new LoginRequest { Identifier = "lock-user", Password = "incorrecta" };

        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Null(await service.LoginAsync(wrong, CancellationToken.None));

        // Bloqueada: ni siquiera la contraseña correcta entra.
        var locked = await Assert.ThrowsAsync<AccountLockedException>(() =>
            service.LoginAsync(new LoginRequest { Identifier = "lock-user", Password = "Password1" }, CancellationToken.None));
        Assert.True(locked.RetryAfter > TimeSpan.FromMinutes(14));

        user.LockoutUntilUtc = DateTime.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
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
        var service = CreateService(db);

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
        var service = CreateService(db);

        for (var attempt = 0; attempt < 7; attempt++)
            Assert.Null(await service.LoginAsync(new LoginRequest { Identifier = "no-existe", Password = "x" }, CancellationToken.None));
    }

    [Fact]
    public async Task LoginAsync_rejects_inactive_user()
    {
        await using var db = CreateContext();
        var user = CreateUser("20523682786", "inactive", "Proveedor Inactivo", "inactive@demo.test");
        user.IsActive = false;
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = CreateService(db);

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
        var service = CreateService(db);

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
        var service = CreateService(db);
        var request = new PasswordResetConfirmRequest { Ruc = user.Ruc!, Token = token, NewPassword = "NewPassword1" };

        var firstConfirmation = await service.ConfirmPasswordResetAsync(request, PasswordTokenPurpose.PasswordReset, CancellationToken.None);
        var secondConfirmation = await service.ConfirmPasswordResetAsync(request, PasswordTokenPurpose.PasswordReset, CancellationToken.None);

        Assert.True(firstConfirmation);
        Assert.False(secondConfirmation);
        Assert.NotNull(db.PasswordResetTokens.Single().UsedAtUtc);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_creates_password_reset_token_and_sends_email()
    {
        await using var db = CreateContext();
        var user = CreateUser("20523682789", "reset-request", "Proveedor Solicitud", "request@demo.test");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var emailSender = new FakeEmailSender();
        var service = CreateService(db, emailSender);

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
        var user = CreateUser("20523682780", "registered", "Proveedor Registrado", "registered@demo.test");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var sap = new SapProviderClient(new HttpClient(new ThrowingHandler()), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Sap:BaseUrl"] = "http://sap.invalid",
            ["Sap:BasicToken"] = "test-token",
        }).Build());
        var service = new OnlineRegistrationService(new EfIdentityRepository(db), new FakeEmailSender(), sap, new ConfigurationBuilder().Build());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ValidateRucAsync(user.Ruc!, CancellationToken.None));

        Assert.Equal("El usuario ya se encuentra registrado.", exception.Message);
    }

    private static AuthService CreateService(AppDbContext db, IEmailSender? emailSender = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = "test-signing-key-with-at-least-32-characters",
            ["Jwt:Issuer"] = "test-issuer",
            ["Jwt:Audience"] = "test-audience",
            ["Jwt:AccessTokenMinutes"] = "30",
            ["Frontend:BaseUrl"] = "http://localhost:4200",
        }).Build();
        return new AuthService(new EfIdentityRepository(db), configuration, emailSender ?? new FakeEmailSender());
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"auth-tests-{Guid.NewGuid():N}")
        .Options);

    private static AppUser CreateUser(string ruc, string username, string company, string email)
    {
        var user = new AppUser { Ruc = ruc, Username = username, CompanyName = company };
        user.PasswordHash = new PasswordHasher<AppUser>().HashPassword(user, "Password1");
        user.Emails.Add(new UserEmail { Email = email, IsPrimary = true });
        var role = new Role { Code = SecurityCatalog.ProviderRole, Name = "Proveedor" };
        user.UserRoles.Add(new UserRole { Role = role });
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

        public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken, bool isHtml = false)
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
}
