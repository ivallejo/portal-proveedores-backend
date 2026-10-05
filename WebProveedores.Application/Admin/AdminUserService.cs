using Microsoft.AspNetCore.Identity;
using WebProveedores.Application.Admin;
using WebProveedores.Application.Abstractions.Persistence;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Entities;
namespace WebProveedores.Application.Admin;

public sealed class AdminUserService(IIdentityRepository db) : IAdminUserService
{
    private readonly PasswordHasher<AppUser> passwordHasher = new();

    public async Task<IReadOnlyList<AdminUserResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var users = await db.ListUsersAsync(cancellationToken);
        return users.Select(ToResponse).ToArray();
    }

    public async Task<AdminUserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var ruc = string.IsNullOrWhiteSpace(request.Ruc) ? null : request.Ruc.Trim();
        if (await db.EmailExistsAsync(email, cancellationToken) || (ruc is not null && await db.UserExistsByRucAsync(ruc, cancellationToken)))
            throw new InvalidOperationException("Ya existe un usuario con ese correo o RUC.");

        var role = await FindRoleAsync(request.Role, cancellationToken);
        var username = string.IsNullOrWhiteSpace(request.Username) ? (ruc ?? email.Split('@')[0]) : request.Username.Trim();
        if (await db.UsernameExistsAsync(username, cancellationToken)) throw new InvalidOperationException("Ya existe un usuario con ese username.");
        // La contraseña la define el administrador: es temporal y la persona debe cambiarla al ingresar.
        var user = new AppUser { Username = username, CompanyName = request.CompanyName.Trim(), Ruc = ruc, PasswordSetAtUtc = DateTime.UtcNow, MustChangePassword = true };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        user.Emails.Add(new UserEmail { Email = email, IsPrimary = true });
        user.UserRoles.Add(new UserRole { Role = role });
        user.SetCompanies(await ResolveCompaniesAsync(request.CompanyCodes, allIfEmpty: true, cancellationToken));
        db.AddUser(user);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    public async Task<AdminUserResponse?> AssignRoleAsync(Guid id, AssignRoleRequest request, CancellationToken cancellationToken)
    {
        var user = await db.FindTrackedByIdAsync(id, cancellationToken);
        if (user is null) return null;
        var role = await FindRoleAsync(request.Role, cancellationToken);
        user.UserRoles.Clear();
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, Role = role });
        user.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    public async Task<AdminUserResponse?> AssignCompaniesAsync(Guid id, AssignCompaniesRequest request, CancellationToken cancellationToken)
    {
        var user = await db.FindTrackedByIdAsync(id, cancellationToken);
        if (user is null) return null;
        user.SetCompanies(await ResolveCompaniesAsync(request.CompanyCodes, allIfEmpty: false, cancellationToken));
        user.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    public async Task<AdminUserResponse?> SetStatusAsync(Guid id, UpdateUserStatusRequest request, CancellationToken cancellationToken)
    {
        var user = await db.FindTrackedByIdAsync(id, cancellationToken);
        if (user is null) return null;
        user.IsActive = request.IsActive;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    private async Task<IReadOnlyList<Company>> ResolveCompaniesAsync(IReadOnlyList<string> codes, bool allIfEmpty, CancellationToken cancellationToken)
    {
        var active = await db.ListActiveCompaniesAsync(cancellationToken);
        var wanted = codes.Select(code => code.Trim()).Where(code => code.Length > 0).Distinct().ToArray();
        if (wanted.Length == 0)
            return allIfEmpty ? active : throw new ArgumentException("Asigna al menos una sociedad.");
        var unknown = wanted.Where(code => active.All(company => company.Code != code)).ToArray();
        if (unknown.Length > 0) throw new ArgumentException($"Sociedad no válida: {string.Join(", ", unknown)}.");
        return active.Where(company => wanted.Contains(company.Code)).ToArray();
    }

    private async Task<Role> FindRoleAsync(string value, CancellationToken cancellationToken)
    {
        var role = await db.FindRoleAsync(value, cancellationToken);
        return role ?? throw new InvalidOperationException("El rol indicado no es válido.");
    }

    private static AdminUserResponse ToResponse(AppUser user)
    {
        var email = user.Emails.FirstOrDefault(item => item.IsPrimary && item.IsActive)?.Email ?? user.Emails.FirstOrDefault(item => item.IsActive)?.Email ?? string.Empty;
        var roles = user.UserRoles.Where(item => item.Role.IsActive).Select(item => item.Role.Name).OrderBy(name => name).ToArray();
        var companies = user.UserCompanies.Select(item => item.Company.Code).OrderBy(code => code).ToArray();
        return new AdminUserResponse(user.Id, user.Username, email, user.CompanyName, user.Ruc ?? string.Empty, roles.FirstOrDefault() ?? string.Empty, roles, companies, user.IsActive, user.CreatedAtUtc);
    }
}
