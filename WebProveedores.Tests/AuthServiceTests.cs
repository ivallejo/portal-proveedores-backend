using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WebProveedores.Application.Auth;
using WebProveedores.Domain.Entities;
using WebProveedores.Infrastructure.Auth;
using WebProveedores.Infrastructure.Persistence;

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
        return new AuthService(db, configuration, emailSender ?? new FakeEmailSender());
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
}
