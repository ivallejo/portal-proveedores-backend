using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Entities;

namespace WebProveedores.Infrastructure.Persistence;

/// <summary>
/// Carga los datos de referencia al iniciar. Los roles del catálogo y las cuatro sociedades base siempre existen.
/// Si hay un archivo de seed (<c>Seed:FilePath</c> o <c>seed.json</c>) agrega además las sociedades
/// con su RUC, las áreas y los usuarios reales. Es idempotente y nunca modifica ni borra registros
/// existentes (solo completa el RUC de una sociedad que aún no lo tiene).
/// Los usuarios se crean con una contraseña temporal y la obligación de cambiarla al ingresar.
/// </summary>
public sealed class ReferenceDataSeeder(AppDbContext db, IConfiguration configuration, ILogger<ReferenceDataSeeder> logger)
{
    private static readonly (string Code, string Name)[] BaseCompanies =
    [
        ("1001", "Naviera Transoceánica"),
        ("1002", "Ultratag"),
        ("1003", "Petral"),
        ("1007", "RENADSA"),
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync(cancellationToken);
        await SeedBaseCompaniesAsync(cancellationToken);

        var path = ResolveSeedFile();
        if (path is null)
        {
            logger.LogInformation("Sin archivo de seed: solo se cargaron las sociedades base.");
            return;
        }

        var seed = Read(path);
        var errors = Validate(seed);
        if (errors.Count > 0)
            throw new InvalidOperationException($"El archivo de seed «{path}» no es válido:{Environment.NewLine}- {string.Join(Environment.NewLine + "- ", errors)}");

        await ApplyCompaniesAsync(seed, cancellationToken);
        await ApplyAreasAsync(seed, cancellationToken);
        await ApplyUsersAsync(seed, cancellationToken);
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        var existing = await db.Roles.Select(role => role.Code).ToListAsync(cancellationToken);
        foreach (var role in SecurityCatalog.Roles.Where(role => !existing.Contains(role.Key)))
            db.Roles.Add(new Role { Code = role.Key, Name = role.Value });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedBaseCompaniesAsync(CancellationToken cancellationToken)
    {
        var existing = await db.Companies.Select(company => company.Code).ToListAsync(cancellationToken);
        foreach (var (code, name) in BaseCompanies.Where(company => !existing.Contains(company.Code)))
            db.Companies.Add(new Company { Code = code, Name = name });
        await db.SaveChangesAsync(cancellationToken);
    }

    private string? ResolveSeedFile()
    {
        // Una ruta relativa se busca en el directorio actual y en el padre (raíz del repo al ejecutar desde WebProveedores.Api).
        var configured = configuration["Seed:FilePath"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var candidates = Path.IsPathRooted(configured) ? [configured] : new[] { configured, Path.Combine("..", configured) };
            return candidates.Select(Path.GetFullPath).FirstOrDefault(File.Exists)
                ?? throw new FileNotFoundException($"No se encontró el archivo de seed configurado en Seed:FilePath: {Path.GetFullPath(configured)}");
        }
        return new[] { "seed.json", Path.Combine("..", "seed.json") }.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
    }

    private static SeedFile Read(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<SeedFile>(File.ReadAllText(path), JsonOptions) ?? new SeedFile();
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"El archivo de seed «{path}» no es un JSON válido: {exception.Message}", exception);
        }
    }

    private List<string> Validate(SeedFile seed)
    {
        var errors = new List<string>();
        var knownRoles = SecurityCatalog.Roles.Keys.ToHashSet();
        var fallbackPassword = configuration["Seed:TemporaryPassword"];

        foreach (var company in seed.Companies)
        {
            if (string.IsNullOrWhiteSpace(company.Code)) errors.Add("Hay una sociedad sin código.");
            if (company.Ruc is { Length: > 0 } ruc && !IsRuc(ruc)) errors.Add($"La sociedad «{company.Code}» tiene un RUC que no es de 11 dígitos.");
        }
        foreach (var duplicate in seed.Users.GroupBy(user => user.Username?.Trim().ToLowerInvariant()).Where(group => group.Count() > 1))
            errors.Add($"El usuario «{duplicate.Key}» está repetido.");

        var areaNames = seed.Areas.Select(area => area.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var user in seed.Users)
        {
            var label = string.IsNullOrWhiteSpace(user.Username) ? "(sin usuario)" : user.Username;
            if (string.IsNullOrWhiteSpace(user.Username)) errors.Add("Hay un usuario sin «username».");
            if (string.IsNullOrWhiteSpace(user.Name)) errors.Add($"El usuario «{label}» no tiene nombre.");
            if (string.IsNullOrWhiteSpace(user.Email) || !user.Email.Contains('@')) errors.Add($"El usuario «{label}» no tiene un correo válido.");
            if (user.Role is null || !knownRoles.Contains(user.Role)) errors.Add($"El usuario «{label}» tiene un rol desconocido. Roles válidos: {string.Join(", ", knownRoles)}.");
            if (user.Role == SecurityCatalog.ProviderRole && !IsRuc(user.Ruc)) errors.Add($"El proveedor «{label}» necesita un RUC de 11 dígitos.");
            if (user.Role == SecurityCatalog.AreaApproverRole && string.IsNullOrWhiteSpace(user.Area)) errors.Add($"El aprobador «{label}» necesita un área.");
            if (!string.IsNullOrWhiteSpace(user.Area) && !areaNames.Contains(user.Area.Trim()))
                errors.Add($"El usuario «{label}» usa el área «{user.Area}», que no está en la lista «areas».");
            if (!MeetsPolicy(user.TemporaryPassword ?? fallbackPassword))
                errors.Add($"El usuario «{label}» necesita una contraseña temporal de mínimo 8 caracteres con mayúscula, minúscula y número (en «temporaryPassword» o en Seed:TemporaryPassword).");
        }
        return errors;
    }

    private async Task ApplyCompaniesAsync(SeedFile seed, CancellationToken cancellationToken)
    {
        var companies = await db.Companies.ToDictionaryAsync(company => company.Code, cancellationToken);
        foreach (var item in seed.Companies)
        {
            var code = item.Code.Trim();
            if (!companies.TryGetValue(code, out var company))
            {
                company = new Company { Code = code, Name = item.Name?.Trim() is { Length: > 0 } name ? name : code, Ruc = item.Ruc?.Trim() };
                db.Companies.Add(company);
                companies[code] = company;
            }
            else if (string.IsNullOrWhiteSpace(company.Ruc) && IsRuc(item.Ruc))
            {
                company.Ruc = item.Ruc!.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(item.Ruc) && company.Ruc != item.Ruc.Trim())
            {
                logger.LogWarning("La sociedad {Code} ya tiene otro RUC; el seed no lo modifica.", code);
            }
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyAreasAsync(SeedFile seed, CancellationToken cancellationToken)
    {
        var existing = (await db.Areas.Select(area => area.Name).ToListAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in seed.Areas.Select(area => area.Trim()).Where(name => name.Length > 0 && !existing.Contains(name)).Distinct(StringComparer.OrdinalIgnoreCase))
            db.Areas.Add(new Area { Code = CodeFor(name), Name = name });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyUsersAsync(SeedFile seed, CancellationToken cancellationToken)
    {
        var roles = await db.Roles.ToDictionaryAsync(role => role.Code, cancellationToken);
        var areas = await db.Areas.ToDictionaryAsync(area => area.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var usernames = (await db.Users.Select(user => user.Username).ToListAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hasher = new PasswordHasher<AppUser>();
        var created = 0;

        foreach (var item in seed.Users)
        {
            var username = item.Username.Trim();
            var email = item.Email.Trim().ToLowerInvariant();
            if (usernames.Contains(username) || await db.UserEmails.AnyAsync(existing => existing.Email == email, cancellationToken)
                || (IsRuc(item.Ruc) && await db.Users.AnyAsync(user => user.Ruc == item.Ruc!.Trim(), cancellationToken)))
            {
                logger.LogInformation("El usuario {Username} ya existe; el seed no lo modifica.", username);
                continue;
            }
            if (!roles.TryGetValue(item.Role, out var role))
                throw new InvalidOperationException($"El rol {item.Role} no existe en la base de datos. Ejecuta primero el arranque que lo crea.");

            var user = new AppUser
            {
                Username = username,
                CompanyName = item.Name.Trim(),
                Ruc = IsRuc(item.Ruc) ? item.Ruc!.Trim() : null,
                Area = string.IsNullOrWhiteSpace(item.Area) ? null : areas[item.Area.Trim()],
                PasswordSetAtUtc = DateTime.UtcNow,
                MustChangePassword = true,
            };
            user.PasswordHash = hasher.HashPassword(user, item.TemporaryPassword ?? configuration["Seed:TemporaryPassword"]!);
            user.Emails.Add(new UserEmail { Email = email, IsPrimary = true });
            user.UserRoles.Add(new UserRole { RoleId = role.Id });
            db.Users.Add(user);
            usernames.Add(username);
            created++;
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seed de usuarios: {Created} creado(s), {Skipped} ya existían.", created, seed.Users.Count - created);
    }

    private static bool IsRuc(string? value) => value is { Length: 11 } && value.All(char.IsAsciiDigit);

    private static bool MeetsPolicy(string? password) =>
        password is { Length: >= 8 } && password.Any(char.IsUpper) && password.Any(char.IsLower) && password.Any(char.IsDigit);

    private static string CodeFor(string name)
    {
        var normalized = name.Normalize(NormalizationForm.FormD)
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark);
        var code = string.Concat(normalized).ToUpperInvariant();
        return string.Concat(code.Select(character => char.IsLetterOrDigit(character) ? character : '_')).Trim('_');
    }

    private sealed class SeedFile
    {
        public List<SeedCompany> Companies { get; init; } = [];
        public List<string> Areas { get; init; } = [];
        public List<SeedUser> Users { get; init; } = [];
    }

    private sealed class SeedCompany
    {
        public string Code { get; init; } = string.Empty;
        public string? Name { get; init; }
        public string? Ruc { get; init; }
    }

    private sealed class SeedUser
    {
        public string Username { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public string? Area { get; init; }
        public string? Ruc { get; init; }
        public string? TemporaryPassword { get; init; }
    }
}
