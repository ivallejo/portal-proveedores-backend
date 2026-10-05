using Microsoft.AspNetCore.Identity;
using WebProveedores.Application.Abstractions.Persistence;
using WebProveedores.Application.Auth;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Entities;

namespace WebProveedores.Application.Admin;

/// <summary>
/// Administración de usuarios: roles, área y sociedades. Reglas: el aprobador necesita área, el proveedor RUC,
/// y todo usuario que no sea administrador al menos una sociedad. Nunca queda el portal sin un administrador activo.
/// </summary>
public sealed class AdminUserService(IIdentityRepository db) : IAdminUserService
{
    private readonly PasswordHasher<AppUser> passwordHasher = new();

    public async Task<AdminUserPage> SearchAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await db.SearchUsersAsync(search, page, pageSize, cancellationToken);
        return new AdminUserPage(result.Items.Select(ToResponse).ToArray(), result.Total, page, pageSize);
    }

    public async Task<AdminCatalogResponse> CatalogAsync(CancellationToken cancellationToken)
    {
        var roles = (await db.ListRolesAsync(cancellationToken))
            .OrderBy(role => SecurityCatalog.Roles.Keys.ToList().IndexOf(role.Code))
            .Select(role => new AdminOption(role.Code, role.Name)).ToArray();
        var areas = (await db.ListActiveAreasAsync(cancellationToken)).Select(area => new AdminAreaOption(area.Id, area.Name)).ToArray();
        var companies = (await db.ListActiveCompaniesAsync(cancellationToken)).Select(company => new AdminOption(company.Code, company.Name)).ToArray();
        return new AdminCatalogResponse(roles, areas, companies);
    }

    public async Task<AdminUserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var ruc = string.IsNullOrWhiteSpace(request.Ruc) ? null : request.Ruc.Trim();
        if (ruc is not null && !IsRuc(ruc)) throw new ArgumentException("El RUC debe tener 11 dígitos.");
        if (!PasswordPolicy.IsSatisfiedBy(request.Password)) throw new ArgumentException(PasswordPolicy.Description);
        if (await db.UsernameExistsAsync(username, cancellationToken)) throw new InvalidOperationException("Ya existe un usuario con ese nombre de usuario.");
        if (await db.EmailExistsAsync(email, cancellationToken)) throw new InvalidOperationException("Ya existe un usuario con ese correo.");
        if (ruc is not null && await db.UserExistsByRucAsync(ruc, cancellationToken)) throw new InvalidOperationException("Ya existe un usuario con ese RUC.");

        // La contraseña la define el administrador: es temporal y la persona debe cambiarla al ingresar.
        var user = new AppUser { Username = username, CompanyName = request.Name.Trim(), Ruc = ruc, PasswordSetAtUtc = DateTime.UtcNow, MustChangePassword = true };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        user.Emails.Add(new UserEmail { Email = email, IsPrimary = true });
        await ApplyAccessAsync(user, request.Roles, request.AreaId, request.CompanyCodes, cancellationToken);
        db.AddUser(user);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    public async Task<AdminUserResponse?> UpdateAsync(Guid actorId, Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await db.FindTrackedByIdAsync(id, cancellationToken);
        if (user is null) return null;

        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.EmailUsedByOtherAsync(email, user.Id, cancellationToken)) throw new InvalidOperationException("Ese correo ya pertenece a otro usuario.");

        var losesAdministrator = IsAdministrator(user) && !request.Roles.Contains(SecurityCatalog.AdministratorRole);
        if (losesAdministrator && user.Id == actorId) throw new InvalidOperationException("No puedes quitarte el rol de administrador.");
        if (losesAdministrator && user.IsActive) await EnsureAnotherAdministratorAsync(user.Id, cancellationToken);

        user.CompanyName = request.Name.Trim();
        var primary = user.Emails.FirstOrDefault(item => item.IsPrimary) ?? user.Emails.FirstOrDefault();
        if (primary is null) user.Emails.Add(new UserEmail { Email = email, IsPrimary = true });
        else primary.Email = email;
        await ApplyAccessAsync(user, request.Roles, request.AreaId, request.CompanyCodes, cancellationToken);
        user.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    public async Task<AdminUserResponse?> SetStatusAsync(Guid actorId, Guid id, UpdateUserStatusRequest request, CancellationToken cancellationToken)
    {
        var user = await db.FindTrackedByIdAsync(id, cancellationToken);
        if (user is null) return null;
        if (!request.IsActive && user.IsActive)
        {
            if (user.Id == actorId) throw new InvalidOperationException("No puedes desactivar tu propia cuenta.");
            if (IsAdministrator(user)) await EnsureAnotherAdministratorAsync(user.Id, cancellationToken);
        }
        user.IsActive = request.IsActive;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    public async Task<AdminUserResponse?> UnlockAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await db.FindTrackedByIdAsync(id, cancellationToken);
        if (user is null) return null;
        user.FailedLoginCount = 0;
        user.LockoutUntilUtc = null;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    private async Task ApplyAccessAsync(AppUser user, IReadOnlyList<string> roleCodes, Guid? areaId, IReadOnlyList<string> companyCodes, CancellationToken cancellationToken)
    {
        var codes = roleCodes.Select(code => code.Trim()).Where(code => code.Length > 0).Distinct().ToArray();
        if (codes.Length == 0) throw new ArgumentException("Asigna al menos un rol.");
        var known = await db.ListRolesAsync(cancellationToken);
        var unknown = codes.Where(code => known.All(role => role.Code != code)).ToArray();
        if (unknown.Length > 0) throw new ArgumentException($"Rol no válido: {string.Join(", ", unknown)}.");

        if (codes.Contains(SecurityCatalog.AreaApproverRole) && areaId is null) throw new ArgumentException("El aprobador de área necesita un área.");
        if (codes.Contains(SecurityCatalog.ProviderRole) && !IsRuc(user.Ruc)) throw new ArgumentException("El proveedor necesita un RUC de 11 dígitos.");

        var area = areaId is { } value
            ? await db.FindAreaAsync(value, cancellationToken) ?? throw new ArgumentException("El área seleccionada no es válida.")
            : null;

        var active = await db.ListActiveCompaniesAsync(cancellationToken);
        var wanted = companyCodes.Select(code => code.Trim()).Where(code => code.Length > 0).Distinct().ToArray();
        var invalid = wanted.Where(code => active.All(company => company.Code != code)).ToArray();
        if (invalid.Length > 0) throw new ArgumentException($"Sociedad no válida: {string.Join(", ", invalid)}.");
        // El administrador trabaja con todas las sociedades; los demás necesitan al menos una.
        if (wanted.Length == 0 && !codes.Contains(SecurityCatalog.AdministratorRole)) throw new ArgumentException("Asigna al menos una sociedad.");

        var roles = known.Where(role => codes.Contains(role.Code)).ToArray();
        foreach (var stale in user.UserRoles.Where(item => roles.All(role => role.Id != item.RoleId)).ToList())
            user.UserRoles.Remove(stale);
        foreach (var role in roles.Where(role => user.UserRoles.All(item => item.RoleId != role.Id)))
            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, Role = role });

        user.Area = area;
        user.AreaId = area?.Id;
        user.SetCompanies(active.Where(company => wanted.Contains(company.Code)));
    }

    private async Task EnsureAnotherAdministratorAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!await db.OtherActiveAdministratorExistsAsync(userId, cancellationToken))
            throw new InvalidOperationException("Debe quedar al menos un administrador activo.");
    }

    private static bool IsAdministrator(AppUser user) =>
        user.UserRoles.Any(item => item.Role?.Code == SecurityCatalog.AdministratorRole);

    private static bool IsRuc(string? value) => value is { Length: 11 } && value.All(char.IsAsciiDigit);

    private static AdminUserResponse ToResponse(AppUser user)
    {
        var email = user.Emails.FirstOrDefault(item => item.IsPrimary && item.IsActive)?.Email ?? user.Emails.FirstOrDefault(item => item.IsActive)?.Email ?? string.Empty;
        var roles = user.UserRoles.Where(item => item.Role.IsActive).Select(item => item.Role.Code)
            .OrderBy(code => SecurityCatalog.Roles.Keys.ToList().IndexOf(code)).ToArray();
        var companies = user.UserCompanies.Select(item => item.Company.Code).OrderBy(code => code).ToArray();
        return new AdminUserResponse(
            user.Id, user.Username, email, user.CompanyName, user.Ruc, user.AreaId, user.Area?.Name, roles, companies,
            user.IsActive, user.LockoutUntilUtc > DateTime.UtcNow, user.MustChangePassword, user.CreatedAtUtc);
    }
}
