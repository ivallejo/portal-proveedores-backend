using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Auth;
using WebProveedores.Domain.Entities;
using WebProveedores.Infrastructure.Persistence;
using WebProveedores.Infrastructure.Providers;

namespace WebProveedores.Infrastructure.Auth;

public sealed class OnlineRegistrationService(
    AppDbContext db,
    IEmailSender emailSender,
    SapProviderClient sapProvider) : IOnlineRegistrationService
{
    private readonly PasswordHasher<AppUser> passwordHasher = new();

    public async Task<ProviderLookupResponse> ValidateRucAsync(string ruc, CancellationToken cancellationToken)
    {
        var normalizedRuc = NormalizeRuc(ruc);
        var provider = await FindProviderAsync(normalizedRuc, cancellationToken);
        return new(normalizedRuc, provider.CompanyName, ObfuscateEmail(provider.Correo!));
    }

    public async Task<AccessKeyResponse> RequestAccessKeyAsync(string ruc, CancellationToken cancellationToken)
    {
        var normalizedRuc = NormalizeRuc(ruc);
        var provider = await FindProviderAsync(normalizedRuc, cancellationToken);
        var user = await db.Users.Include(item => item.Emails).Include(item => item.UserRoles).SingleOrDefaultAsync(item => item.Ruc == normalizedRuc, cancellationToken);
        var temporaryPassword = GenerateTemporaryPassword();

        if (user is null)
        {
            user = new AppUser
            {
                Username = normalizedRuc,
                Ruc = normalizedRuc,
                CompanyName = provider.CompanyName.Trim(),
            };
            user.Emails.Add(new UserEmail { Email = provider.Correo!.Trim().ToLowerInvariant(), IsPrimary = true });
            user.UserRoles.Add(new UserRole { Role = await db.Roles.SingleAsync(role => role.Code == SecurityCatalog.ProviderRole, cancellationToken) });
            db.Users.Add(user);
        }
        else
        {
            var email = user.Emails.FirstOrDefault(item => item.IsPrimary && item.IsActive) ?? user.Emails.FirstOrDefault(item => item.IsActive);
            if (email is null) user.Emails.Add(new UserEmail { Email = provider.Correo!.Trim().ToLowerInvariant(), IsPrimary = true });
            else email.Email = provider.Correo!.Trim().ToLowerInvariant();
            user.CompanyName = provider.CompanyName.Trim();
            user.IsActive = true;
            user.UpdatedAtUtc = DateTime.UtcNow;
        }

        user.PasswordHash = passwordHasher.HashPassword(user, temporaryPassword);
        await db.SaveChangesAsync(cancellationToken);
        await emailSender.SendAsync(provider.Correo!, "Tu cuenta está lista - Portal de Proveedores", EmailTemplates.AccessKey(provider.CompanyName, temporaryPassword), cancellationToken, isHtml: true);

        return new(true, ObfuscateEmail(provider.Correo!));
    }

    private async Task<SapProviderRecord> FindProviderAsync(string ruc, CancellationToken cancellationToken)
    {
        var provider = await sapProvider.FindByRucAsync(ruc, cancellationToken);
        if (provider is null || string.IsNullOrWhiteSpace(provider.Correo) || string.IsNullOrWhiteSpace(provider.CompanyName))
            throw new KeyNotFoundException("No encontramos información para el RUC indicado.");
        return provider;
    }

    private static string NormalizeRuc(string ruc) =>
        System.Text.RegularExpressions.Regex.IsMatch(ruc.Trim(), "^\\d{11}$")
            ? ruc.Trim()
            : throw new KeyNotFoundException("Ingresa un RUC válido de 11 dígitos.");

    private static string ObfuscateEmail(string email)
    {
        var parts = email.Split('@', 2);
        if (parts.Length != 2) return email;
        var domainParts = parts[1].Split('.', 2);
        var domainName = domainParts[0];
        var suffix = domainParts.Length > 1 ? $".{domainParts[1]}" : string.Empty;
        return $"{parts[0][..Math.Min(3, parts[0].Length)]}*****{domainName[^Math.Min(3, domainName.Length)..]}{suffix}";
    }

    private static string GenerateTemporaryPassword() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(9)).Replace("/", "A").Replace("+", "B")[..12] + "!a1";

}
