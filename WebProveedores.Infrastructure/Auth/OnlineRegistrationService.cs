using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WebProveedores.Application.Auth;
using WebProveedores.Domain.Entities;
using WebProveedores.Infrastructure.Persistence;

namespace WebProveedores.Infrastructure.Auth;

public sealed class OnlineRegistrationService(
    AppDbContext db,
    IEmailSender emailSender,
    IWebHostEnvironment environment) : IOnlineRegistrationService
{
    private readonly PasswordHasher<AppUser> passwordHasher = new();

    public ProviderLookupResponse ValidateRuc(string ruc)
    {
        var provider = FindProvider(ruc);
        return new(provider.Ruc, provider.CompanyName, ObfuscateEmail(provider.Email));
    }

    public async Task<AccessKeyResponse> RequestAccessKeyAsync(string ruc, CancellationToken cancellationToken)
    {
        var provider = FindProvider(ruc);
        var normalizedRuc = provider.Ruc;
        var user = await db.Users.SingleOrDefaultAsync(item => item.Ruc == normalizedRuc, cancellationToken);
        var temporaryPassword = GenerateTemporaryPassword();

        if (user is null)
        {
            user = new AppUser
            {
                Username = normalizedRuc,
                Ruc = normalizedRuc,
                Email = provider.Email.Trim().ToLowerInvariant(),
                CompanyName = provider.CompanyName.Trim(),
                Role = "Proveedor",
            };
            db.Users.Add(user);
        }
        else
        {
            user.Email = provider.Email.Trim().ToLowerInvariant();
            user.CompanyName = provider.CompanyName.Trim();
            user.IsActive = true;
        }

        user.PasswordHash = passwordHasher.HashPassword(user, temporaryPassword);
        await db.SaveChangesAsync(cancellationToken);
        await emailSender.SendAsync(provider.Email, "Clave de acceso - Portal de Proveedores", $"Tu clave temporal de acceso es: {temporaryPassword}", cancellationToken);

        return new(true, ObfuscateEmail(provider.Email));
    }

    private MockProvider FindProvider(string ruc)
    {
        var normalizedRuc = ruc.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(normalizedRuc, "^\\d{11}$"))
            throw new KeyNotFoundException("Ingresa un RUC válido de 11 dígitos.");

        var filePath = Path.Combine(environment.ContentRootPath, "..", "WebProveedores.Infrastructure", "Mocks", "providers.json");
        if (!File.Exists(filePath)) filePath = Path.Combine(AppContext.BaseDirectory, "Mocks", "providers.json");
        var providers = JsonSerializer.Deserialize<List<MockProvider>>(
            File.ReadAllText(filePath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        return providers.SingleOrDefault(item => item.Ruc == normalizedRuc)
            ?? throw new KeyNotFoundException("No encontramos información para el RUC indicado.");
    }

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

    private sealed record MockProvider(string Ruc, string CompanyName, string Email);
}
