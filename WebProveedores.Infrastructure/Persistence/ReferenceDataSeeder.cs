using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebProveedores.Application.Ports.Outbound.Security;
using WebProveedores.Domain.Access;
using WebProveedores.Domain.Identity;
using WebProveedores.Domain.Organization;

namespace WebProveedores.Infrastructure.Persistence;

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
        var errors = Validate(seed);
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

    private List<string> Validate(SeedFile seed)
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
            else if (string.IsNullOrWhiteSpace(area.Company)) errors.Add($"El área «{area.Name}» necesita «company» (código de la sociedad a la que pertenece).");
            else if (!companyCodesForAreas.Contains(area.Company.Trim())) errors.Add($"El área «{area.Name}» usa la sociedad «{area.Company}», que no existe.");
        }
        foreach (var duplicate in seed.Areas.GroupBy(area => $"{area.Company?.Trim()}:{area.Name?.Trim().ToLowerInvariant()}").Where(group => group.Count() > 1))
            errors.Add($"El área «{duplicate.First().Name}» está repetida en la misma sociedad.");
        var areaNames = seed.Areas.Where(area => !string.IsNullOrWhiteSpace(area.Name))
            .SelectMany(area => new[] { area.Name.Trim(), $"{area.Company?.Trim()}:{area.Name.Trim()}" })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ambiguousAreas = seed.Areas.Where(area => !string.IsNullOrWhiteSpace(area.Name)).GroupBy(area => area.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var companyCodes = BaseCompanies.Select(company => company.Code).Concat(seed.Companies.Select(company => company.Code.Trim())).ToHashSet();
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
            else if (!string.IsNullOrWhiteSpace(user.Area) && ambiguousAreas.Contains(user.Area.Trim()))
                errors.Add($"El área «{user.Area}» existe en varias sociedades: indícala como «CÓDIGO:Nombre» en el usuario «{label}».");
            foreach (var code in user.Companies ?? [])
                if (!companyCodes.Contains(code.Trim())) errors.Add($"El usuario «{label}» usa la sociedad «{code}», que no es una sociedad base ni está en «companies».");
            if (user.Companies is { Count: 0 }) errors.Add($"El usuario «{label}» tiene «companies» vacío: omítelo para asignar todas o indica al menos una.");
            if (!PasswordPolicy.IsSatisfiedBy(user.TemporaryPassword ?? fallbackPassword))
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
        var existing = (await db.Areas.Select(area => new { area.CompanyId, area.Code }).ToListAsync(cancellationToken))
            .Select(area => (area.CompanyId, area.Code)).ToHashSet();
        foreach (var item in seed.Areas)
        {
            var company = companies[item.Company!.Trim()];
            var code = Area.CodeFor(item.Name);
            if (!existing.Add((company.Id, code))) continue;
            db.Areas.Add(new Area { Code = code, Name = item.Name.Trim(), Description = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim(), CompanyId = company.Id, Company = company });
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyUsersAsync(SeedFile seed, CancellationToken cancellationToken)
    {
        var roles = await db.Roles.ToDictionaryAsync(role => role.Code, cancellationToken);
        var companies = await db.Companies.Where(company => company.IsActive).ToListAsync(cancellationToken);
        var areaList = await db.Areas.Include(area => area.Company).ToListAsync(cancellationToken);
        // «Operaciones» o, si el nombre se repite entre sociedades, «1001:Operaciones».
        Area FindArea(string reference)
        {
            var parts = reference.Trim().Split(':', 2);
            var matches = parts.Length == 2
                ? areaList.Where(area => area.Company.Code == parts[0].Trim() && string.Equals(area.Name, parts[1].Trim(), StringComparison.OrdinalIgnoreCase)).ToList()
                : areaList.Where(area => string.Equals(area.Name, parts[0], StringComparison.OrdinalIgnoreCase)).ToList();
            return matches.Count == 1 ? matches[0] : throw new InvalidOperationException($"El área «{reference}» no existe o se repite entre sociedades: usa «CÓDIGO:Nombre».");
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
            var dni = string.IsNullOrWhiteSpace(item.Dni) ? null : item.Dni.Trim();
            if (dni is not null && (dni.Length != 8 || !dni.All(char.IsAsciiDigit)))
                throw new InvalidOperationException($"El DNI del usuario {username} debe tener 8 dígitos.");
            var user = AppUser.Create(username, item.Name, IsRuc(item.Ruc) ? item.Ruc : null, email, hasher.Hash(password), now, temporaryPassword: true, dni: dni);
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

    private static bool IsRuc(string? value) => value is { Length: 11 } && value.All(char.IsAsciiDigit);


    /// <summary>Área del archivo de seed: nombre, código de su sociedad y descripción opcional.</summary>
    private sealed class SeedArea
    {
        public string Name { get; init; } = string.Empty;
        public string? Company { get; init; }
        public string? Description { get; init; }
    }

    private sealed class SeedFile
    {
        public List<SeedCompany> Companies { get; init; } = [];
        public List<SeedArea> Areas { get; init; } = [];
        public List<SeedUser> Users { get; init; } = [];
    }

    private sealed class SeedCompany
    {
        public string Code { get; init; } = string.Empty;
        public string? Name { get; init; }
        public string? Ruc { get; init; }
        public string? BillingEmail { get; init; }
    }

    private sealed class SeedUser
    {
        public string Username { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public string? Area { get; init; }
        public string? Ruc { get; init; }
        /// <summary>DNI del personal interno (también sirve para ingresar).</summary>
        public string? Dni { get; init; }
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        /// <summary>Códigos de sociedad; si se omite, el usuario trabaja con todas las sociedades activas.</summary>
        public List<string>? Companies { get; init; }
        public string? TemporaryPassword { get; init; }
    }
}
