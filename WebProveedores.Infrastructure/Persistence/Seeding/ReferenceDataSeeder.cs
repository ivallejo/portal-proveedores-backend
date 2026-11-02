using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebProveedores.Application.Ports.Outbound.Security;
using WebProveedores.Domain.Access;
using WebProveedores.Domain.Common;
using WebProveedores.Domain.Identity;
using WebProveedores.Domain.Organization;
using WebProveedores.Infrastructure.Persistence.Seeding.Models;

namespace WebProveedores.Infrastructure.Persistence.Seeding;

/// <summary>
/// Carga los datos de referencia al iniciar. Los roles del catálogo y las cuatro sociedades base siempre existen.
/// Si hay un archivo de seed (<c>Seed:FilePath</c> o <c>seed.json</c>) agrega además las sociedades
/// con su RUC, las áreas y los usuarios reales. Es idempotente y nunca modifica ni borra registros
/// existentes (solo completa el RUC de una sociedad que aún no lo tiene).
/// Los usuarios se crean con una contraseña temporal y la obligación de cambiarla al ingresar.
/// </summary>
public sealed class ReferenceDataSeeder(AppDbContext db, IConfiguration configuration, IPasswordHasher hasher, TimeProvider clock, ILogger<ReferenceDataSeeder> logger)
{
    /// <summary>Sociedades del grupo: código de sociedad SAP, razón social, RUC (confirmados con SAP) y correo de facturación.</summary>
    private static readonly (string Code, string Name, string Ruc, string? BillingEmail)[] BaseCompanies =
    [
        ("1001", "Naviera Transoceánica S.A.", "20522163890", "facturacionreceptor1@navitranso.com"),
        ("1002", "Petrolera Transoceánica S.A.", "20100126606", "facturacionreceptor@petranso.com"),
        ("1003", "Naviera Petral S.A.", "20511922578", "facturacionreceptor@petral.com.pe"),
        ("1007", "Representaciones Navieras y Aduaneras S.A.C.", "20100245796", null),
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
        await SeedMenusAsync(cancellationToken);
        await SeedBaseCompaniesAsync(cancellationToken);

        var path = ResolveSeedFile();
        if (path is null)
        {
            logger.LogInformation("Sin archivo de seed: solo se cargaron las sociedades base.");
            return;
        }

        var seed = Read(path);
        // La contraseña temporal solo se valida para los usuarios que se van a crear: los existentes no se modifican.
        var existingUsernames = (await db.Users.Select(user => user.Username).ToListAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var errors = Validate(seed, existingUsernames);
        if (errors.Count > 0)
            throw new InvalidOperationException($"El archivo de seed «{path}» no es válido:{Environment.NewLine}- {string.Join(Environment.NewLine + "- ", errors)}");

        await ApplyCompaniesAsync(seed, cancellationToken);
        await ApplyAreasAsync(seed, cancellationToken);
        await ApplyUsersAsync(seed, cancellationToken);
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        var existing = await db.Roles.ToDictionaryAsync(role => role.Code, cancellationToken);
        foreach (var (code, name) in SecurityCatalog.Roles)
        {
            if (!existing.TryGetValue(code, out var role)) db.Roles.Add(role = new Role { Code = code, Name = name });
            role.Description ??= SecurityCatalog.Descriptions[code];
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Crea las opciones del sistema que falten y se las asigna a sus roles base. Las existentes no se tocan:
    /// el administrador puede haber cambiado nombres, íconos, orden o permisos.
    /// </summary>
    private async Task SeedMenusAsync(CancellationToken cancellationToken)
    {
        var menus = await db.MenuOptions.ToDictionaryAsync(menu => menu.Code, cancellationToken);
        var roles = await db.Roles.ToDictionaryAsync(role => role.Code, cancellationToken);
        var created = 0;
        foreach (var entry in MenuCatalog.System)
        {
            if (menus.ContainsKey(entry.Code)) continue;
            var menu = new MenuOption
            {
                Code = entry.Code,
                Name = entry.Name,
                Route = entry.Route,
                Icon = entry.Icon,
                Order = entry.Order,
                Parent = entry.Parent is null ? null : menus[entry.Parent],
                CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
            };
            foreach (var code in entry.Roles) menu.RoleMenus.Add(new RoleMenu { Role = roles[code], MenuOption = menu });
            db.MenuOptions.Add(menu);
            menus[entry.Code] = menu;
            created++;
        }
        await db.SaveChangesAsync(cancellationToken);
        if (created > 0) logger.LogInformation("Menús del sistema: {Created} opción(es) creada(s).", created);
    }

    private async Task SeedBaseCompaniesAsync(CancellationToken cancellationToken)
    {
        var existing = await db.Companies.ToDictionaryAsync(company => company.Code, cancellationToken);
        foreach (var (code, name, ruc, billingEmail) in BaseCompanies)
        {
            if (!existing.TryGetValue(code, out var company))
            {
                db.Companies.Add(new Company { Code = code, Name = name, Ruc = ruc, BillingEmail = billingEmail });
                continue;
            }
            // Como con el archivo de seed, solo se completa lo que falta; nunca se cambia un dato existente.
            if (string.IsNullOrWhiteSpace(company.Ruc)) company.Ruc = ruc;
            if (string.IsNullOrWhiteSpace(company.BillingEmail)) company.BillingEmail = billingEmail;
        }
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

    private List<string> Validate(SeedFile seed, IReadOnlySet<string> existingUsernames)
    {
        var errors = new List<string>();
        var knownRoles = SecurityCatalog.Roles.Keys.ToHashSet();
        var fallbackPassword = configuration["Seed:TemporaryPassword"];

        foreach (var company in seed.Companies)
        {
            if (string.IsNullOrWhiteSpace(company.Code)) errors.Add("Hay una sociedad sin código.");
            if (company.Ruc is { Length: > 0 } ruc && !IsRuc(ruc)) errors.Add($"La sociedad «{company.Code}» tiene un RUC que no es de 11 dígitos.");
            if (company.BillingEmail is { Length: > 0 } email && !email.Contains('@')) errors.Add($"La sociedad «{company.Code}» tiene un correo de facturación inválido.");
        }
        foreach (var duplicate in seed.Users.GroupBy(user => user.Username?.Trim().ToLowerInvariant()).Where(group => group.Count() > 1))
            errors.Add($"El usuario «{duplicate.Key}» está repetido.");

        var companyCodesForAreas = BaseCompanies.Select(company => company.Code).Concat(seed.Companies.Select(company => company.Code.Trim())).ToHashSet();
        foreach (var area in seed.Areas)
        {
            if (string.IsNullOrWhiteSpace(area.Name)) errors.Add("Hay un área sin nombre.");
            else if (CompaniesOf(area).Count == 0) errors.Add($"El área «{area.Name}» necesita «companies» (códigos de las sociedades en las que existe).");
            else foreach (var code in CompaniesOf(area).Where(code => !companyCodesForAreas.Contains(code)))
                errors.Add($"El área «{area.Name}» usa la sociedad «{code}», que no existe.");
        }
        // El nombre es único: si se repite (seeds anteriores, una entrada por sociedad), se une en una sola área.
        var areaNames = seed.Areas.Where(area => !string.IsNullOrWhiteSpace(area.Name))
            .SelectMany(area => CompaniesOf(area).Select(code => $"{code}:{area.Name.Trim()}").Prepend(area.Name.Trim()))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var companyCodes = BaseCompanies.Select(company => company.Code).Concat(seed.Companies.Select(company => company.Code.Trim())).ToHashSet();
        foreach (var user in seed.Users)
        {
            var label = string.IsNullOrWhiteSpace(user.Username) ? "(sin usuario)" : user.Username;
            if (string.IsNullOrWhiteSpace(user.Username)) errors.Add("Hay un usuario sin «username».");
            if (string.IsNullOrWhiteSpace(user.Name)) errors.Add($"El usuario «{label}» no tiene nombre.");
            if (string.IsNullOrWhiteSpace(user.Email) || !user.Email.Contains('@')) errors.Add($"El usuario «{label}» no tiene un correo válido.");
            if (user.Role is null || !knownRoles.Contains(user.Role)) errors.Add($"El usuario «{label}» tiene un rol desconocido. Roles válidos: {string.Join(", ", knownRoles)}.");
            if (user.Role == SecurityCatalog.ProviderRole && !IsRuc(user.Ruc)) errors.Add($"El proveedor «{label}» necesita un RUC de 11 dígitos.");
            if (IsRuc(user.Ruc) && user.Username?.Trim() != user.Ruc!.Trim()) errors.Add($"El usuario del proveedor «{label}» debe ser su RUC.");
            if (!IsRuc(user.Ruc) && UsernamePolicy.Violation(user.Username) is { } usernameError) errors.Add($"Usuario «{label}»: {usernameError}");
            if (!IsRuc(user.Ruc) && DocumentError(user) is { } documentError) errors.Add($"El usuario «{label}»: {documentError}");
            if (user.Role == SecurityCatalog.AreaApproverRole && string.IsNullOrWhiteSpace(user.Area)) errors.Add($"El aprobador «{label}» necesita un área.");
            if (!string.IsNullOrWhiteSpace(user.Area) && !areaNames.Contains(user.Area.Trim()))
                errors.Add($"El usuario «{label}» usa el área «{user.Area}», que no está en la lista «areas».");
            foreach (var code in user.Companies ?? [])
                if (!companyCodes.Contains(code.Trim())) errors.Add($"El usuario «{label}» usa la sociedad «{code}», que no es una sociedad base ni está en «companies».");
            if (user.Companies is { Count: 0 }) errors.Add($"El usuario «{label}» tiene «companies» vacío: omítelo para asignar todas o indica al menos una.");
            var violations = existingUsernames.Contains(user.Username.Trim())
                ? []
                : PasswordPolicy.Violations(user.TemporaryPassword ?? fallbackPassword, [user.Username, user.Ruc, user.Dni ?? user.DocumentNumber, user.FirstName, user.LastName, user.Name]);
            if (violations.Count > 0)
                errors.Add($"La contraseña temporal del usuario «{label}» (en «temporaryPassword» o en Seed:TemporaryPassword) no cumple la política: {string.Join(" ", violations)}");
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
                company = new Company { Code = code, Name = item.Name?.Trim() is { Length: > 0 } name ? name : code, Ruc = item.Ruc?.Trim(), BillingEmail = item.BillingEmail?.Trim().ToLowerInvariant() };
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
            if (string.IsNullOrWhiteSpace(company.BillingEmail) && !string.IsNullOrWhiteSpace(item.BillingEmail))
                company.BillingEmail = item.BillingEmail.Trim().ToLowerInvariant();
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyAreasAsync(SeedFile seed, CancellationToken cancellationToken)
    {
        var companies = await db.Companies.ToDictionaryAsync(company => company.Code, cancellationToken);
        var existing = (await db.Areas.Select(area => area.Code).ToListAsync(cancellationToken)).ToHashSet();
        // Las áreas que ya existen no se modifican; las nuevas con el mismo nombre se unen con todas sus sociedades.
        foreach (var group in seed.Areas.GroupBy(item => Area.CodeFor(item.Name)))
        {
            if (!existing.Add(group.Key)) continue;
            var first = group.First();
            var area = new Area { Code = group.Key, Name = first.Name.Trim(), Description = group.Select(item => item.Description).FirstOrDefault(text => !string.IsNullOrWhiteSpace(text))?.Trim() };
            area.SetCompanies(group.SelectMany(CompaniesOf).Distinct().Select(code => companies[code]));
            db.Areas.Add(area);
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyUsersAsync(SeedFile seed, CancellationToken cancellationToken)
    {
        var roles = await db.Roles.ToDictionaryAsync(role => role.Code, cancellationToken);
        var companies = await db.Companies.Where(company => company.IsActive).ToListAsync(cancellationToken);
        var areaList = await db.Areas.Include(area => area.Companies).ThenInclude(item => item.Company).ToListAsync(cancellationToken);
        // «Operaciones» (el nombre es único); «1001:Operaciones» se sigue aceptando si el área está en esa sociedad.
        Area FindArea(string reference)
        {
            var parts = reference.Trim().Split(':', 2);
            var name = parts[^1].Trim();
            var area = areaList.SingleOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            if (area is null || (parts.Length == 2 && !area.CompanyCodes.Contains(parts[0].Trim())))
                throw new InvalidOperationException($"El área «{reference}» no existe.");
            return area;
        }
        var usernames = (await db.Users.Select(user => user.Username).ToListAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = clock.GetUtcNow().UtcDateTime;
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

            var password = item.TemporaryPassword ?? configuration["Seed:TemporaryPassword"]!;
            var user = AppUser.Create(username, item.Name, IsRuc(item.Ruc) ? item.Ruc : null, email, hasher.Hash(password), now, temporaryPassword: true,
                document: IsRuc(item.Ruc) ? null : DocumentOf(item));
            if (!user.IsProvider && !string.IsNullOrWhiteSpace(item.FirstName) && !string.IsNullOrWhiteSpace(item.LastName))
                user.SetPersonName(item.FirstName, item.LastName, now);
            user.AssignArea(string.IsNullOrWhiteSpace(item.Area) ? null : FindArea(item.Area));
            user.SetRoles([role]);
            user.SetCompanies(item.Companies is null ? companies : companies.Where(company => item.Companies.Any(code => code.Trim() == company.Code)));
            db.Users.Add(user);
            usernames.Add(username);
            created++;
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seed de usuarios: {Created} creado(s), {Skipped} ya existían.", created, seed.Users.Count - created);
    }

    /// <summary>Documento del interno: «documentType» + «documentNumber», o «dni» (seeds anteriores). Vacío si no indica ninguno.</summary>
    private static (IdentityDocumentType Type, string Number)? DocumentOf(SeedUser user)
    {
        if (!string.IsNullOrWhiteSpace(user.Dni)) return (IdentityDocumentType.Dni, IdentityDocument.Normalize(IdentityDocumentType.Dni, user.Dni));
        if (string.IsNullOrWhiteSpace(user.DocumentType) && string.IsNullOrWhiteSpace(user.DocumentNumber)) return null;
        IdentityDocumentType type = user.DocumentType?.Trim().ToUpperInvariant() switch
        {
            "DNI" => IdentityDocumentType.Dni,
            "CE" => IdentityDocumentType.ForeignerCard,
            "PASAPORTE" => IdentityDocumentType.Passport,
            _ => throw new DomainRuleException("«documentType» debe ser DNI, CE o PASAPORTE."),
        };
        return (type, IdentityDocument.Normalize(type, user.DocumentNumber));
    }

    private static string? DocumentError(SeedUser user)
    {
        try
        {
            DocumentOf(user);
            return null;
        }
        catch (DomainRuleException exception)
        {
            return exception.Message;
        }
    }

    private static IReadOnlyList<string> CompaniesOf(SeedArea area) =>
        (area.Companies ?? []).Append(area.Company).Where(code => !string.IsNullOrWhiteSpace(code)).Select(code => code!.Trim()).Distinct().ToArray();

    private static bool IsRuc(string? value) => value is { Length: 11 } && value.All(char.IsAsciiDigit);
}
