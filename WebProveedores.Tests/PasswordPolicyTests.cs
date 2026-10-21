using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using WebProveedores.Application.Auth;
using WebProveedores.Application.Auth.Commands;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Application.Common.Security;
using WebProveedores.Application.Ports.Outbound.Notifications;
using WebProveedores.Application.Ports.Outbound.Notifications.Models;
using WebProveedores.Domain.Access;
using WebProveedores.Domain.Identity;
using WebProveedores.Infrastructure.Persistence;
using WebProveedores.Infrastructure.Persistence.Repositories;

namespace WebProveedores.Tests;

/// <summary>Política de contraseñas 3.1 (spec 004): composición, datos personales, palabras comunes, historial y vigencia.</summary>
public sealed class PasswordPolicyTests
{
    private static readonly string?[] Maria = ["maria.torres", null, "45879632", "María José", "Torres Ñahui", "María José Torres Ñahui"];

    [Theory]
    [InlineData("Lunes#Brisa2027")]      // 4 tipos
    [InlineData("brisa marina 2027")]    // minúsculas, números y espacio (especial)
    [InlineData("BRISAMARINA#2027")]     // mayúsculas, números y especial
    [InlineData("CaminoAlSur#xy")]       // mayúsculas, minúsculas y especial
    public void Accepts_twelve_characters_with_three_of_four_kinds(string password) =>
        Assert.Empty(PasswordPolicy.Violations(password, Maria));

    [Theory]
    [InlineData("Brisa#2027", PasswordPolicy.TooShort)]
    [InlineData("brisamarina2027", PasswordPolicy.TooFewKinds)]
    [InlineData("BrisaMarinaSur", PasswordPolicy.TooFewKinds)]
    [InlineData("Maria#Lunes2027", PasswordPolicy.ContainsPersonalData)]   // nombre sin tilde
    [InlineData("Lunes#ÑAHUI2027", PasswordPolicy.ContainsPersonalData)]   // apellido
    [InlineData("Clave#45879632", PasswordPolicy.ContainsPersonalData)]    // DNI
    [InlineData("Torres.Brisa27", PasswordPolicy.ContainsPersonalData)]    // apellido dentro del usuario
    [InlineData("Naviera2026!", PasswordPolicy.TooCommon)]                 // palabra del negocio con números y símbolo al final
    [InlineData("#2026Contraseña", PasswordPolicy.TooCommon)]              // con tilde y números al inicio
    [InlineData("Password1234", PasswordPolicy.TooCommon)]
    [InlineData("Qwertyuiop12!", PasswordPolicy.TooCommon)]
    public void Rejects_each_rule_with_its_own_message(string password, string expected) =>
        Assert.Contains(expected, PasswordPolicy.Violations(password, Maria));

    [Fact]
    public void Ignores_short_words_and_legal_suffixes_of_the_business_name()
    {
        string?[] provider = ["20100128056", "20100128056", null, null, null, "Saga Falabella S.A.C."];

        Assert.Empty(PasswordPolicy.Violations("Sac#Del#Lunes27", provider));
        Assert.Contains(PasswordPolicy.ContainsPersonalData, PasswordPolicy.Violations("Falabella#2027", provider));
    }

    [Fact]
    public async Task Change_rejects_any_of_the_last_five_passwords_and_keeps_only_four_previous()
    {
        await using var db = CreateContext();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
        var user = await AddUserAsync(db, "Inicial#Clave01", clock.GetUtcNow().UtcDateTime);
        var passwords = Passwords(db, clock);

        string[] history = ["Inicial#Clave01", "Segunda#Clave02", "Tercera#Clave03", "Cuarta#Clave04", "Quinta#Clave05"];
        for (var index = 1; index < history.Length; index++)
        {
            clock.Advance(TimeSpan.FromDays(1));
            await passwords.ChangePasswordAsync(user.Id, false, new ChangePasswordCommand { CurrentPassword = history[index - 1], NewPassword = history[index] }, CancellationToken.None);
        }

        foreach (var reused in history)
        {
            var exception = await Assert.ThrowsAsync<ValidationException>(() => passwords.ChangePasswordAsync(user.Id, false,
                new ChangePasswordCommand { CurrentPassword = "Quinta#Clave05", NewPassword = reused }, CancellationToken.None));
            Assert.Equal(PasswordPolicy.Reused, exception.Message);
        }

        clock.Advance(TimeSpan.FromDays(1));
        await passwords.ChangePasswordAsync(user.Id, false, new ChangePasswordCommand { CurrentPassword = "Quinta#Clave05", NewPassword = "Sexta#Clave06x" }, CancellationToken.None);

        // La más antigua salió del historial: vuelve a ser válida; las otras cuatro y la actual, no.
        Assert.Equal(PasswordPolicy.HistorySize - 1, await db.PasswordHistory.CountAsync(entry => entry.UserId == user.Id));
        Assert.DoesNotContain(user.PasswordHistory, entry => TestServices.Hasher.Verify(entry.PasswordHash, "Inicial#Clave01"));
    }

    [Fact]
    public async Task Expired_password_forces_a_change_on_login_without_asking_the_current_one()
    {
        await using var db = CreateContext();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var user = await AddUserAsync(db, "Vigente#Clave01", clock.GetUtcNow().UtcDateTime);
        var login = TestServices.Login(db, clock);

        clock.Advance(TimeSpan.FromDays(PasswordPolicy.ValidityDays));
        var stillValid = await login.LoginAsync(new LoginCommand { Identifier = "maria.torres", Password = "Vigente#Clave01" }, CancellationToken.None);
        Assert.False(stillValid!.User.MustChangePassword);

        clock.Advance(TimeSpan.FromDays(1));
        var expired = await login.LoginAsync(new LoginCommand { Identifier = "maria.torres", Password = "Vigente#Clave01" }, CancellationToken.None);
        Assert.True(expired!.User.MustChangePassword);
        Assert.True(expired.User.PasswordExpired);
        Assert.Contains(new JwtSecurityTokenHandler().ReadJwtToken(expired.AccessToken).Claims, claim => claim.Type == SessionClaims.PasswordChangeOnly);

        var changed = await Passwords(db, clock).ChangePasswordAsync(user.Id, true, new ChangePasswordCommand { NewPassword = "Renovada#Clave02" }, CancellationToken.None);
        Assert.False(changed.User.MustChangePassword);
        Assert.False(changed.User.PasswordExpired);
        Assert.DoesNotContain(new JwtSecurityTokenHandler().ReadJwtToken(changed.AccessToken).Claims, claim => claim.Type == SessionClaims.PasswordChangeOnly);
    }

    private static PasswordService Passwords(AppDbContext db, TimeProvider clock) =>
        new(new EfUserRepository(db), new EfPasswordTokenRepository(db, clock), new EfUnitOfWork(db), TestServices.Hasher, TestServices.Tokens,
            TestServices.Links(db, new NoEmail()), clock);

    private static async Task<AppUser> AddUserAsync(AppDbContext db, string password, DateTime now)
    {
        var user = AppUser.Create("maria.torres", "María Torres", null, "maria@demo.test", TestServices.Hasher.Hash(password), now);
        user.SetRoles([new Role { Code = SecurityCatalog.InternalUserRole, Name = "Usuario interno" }]);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"password-policy-{Guid.NewGuid():N}").Options);

    private sealed class NoEmail : IEmailSender
    {
        public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken, bool isHtml = false, IReadOnlyList<string>? copyTo = null, IReadOnlyList<EmailAttachment>? attachments = null) => Task.CompletedTask;
    }
}
