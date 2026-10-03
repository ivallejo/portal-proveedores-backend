using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Admin;
using WebProveedores.Domain.Entities;
using WebProveedores.Infrastructure.Persistence;

namespace WebProveedores.Infrastructure.Admin;

public sealed class AdminUserService(AppDbContext db) : IAdminUserService
{
    private readonly PasswordHasher<AppUser> passwordHasher = new();

    public async Task<IReadOnlyList<AdminUserResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var users = await db.Users.AsNoTracking().Include(user => user.Emails).Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role).OrderBy(user => user.CompanyName).ToListAsync(cancellationToken);
        return users.Select(ToResponse).ToArray();
    }

    public async Task<AdminUserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var ruc = string.IsNullOrWhiteSpace(request.Ruc) ? null : request.Ruc.Trim();
        if (await db.UserEmails.AnyAsync(item => item.Email == email, cancellationToken) || (ruc is not null && await db.Users.AnyAsync(user => user.Ruc == ruc, cancellationToken)))
            throw new InvalidOperationException("Ya existe un usuario con ese correo o RUC.");

        var role = await FindRoleAsync(request.Role, cancellationToken);
        var username = string.IsNullOrWhiteSpace(request.Username) ? (ruc ?? email.Split('@')[0]) : request.Username.Trim();
        if (await db.Users.AnyAsync(user => user.Username == username, cancellationToken)) throw new InvalidOperationException("Ya existe un usuario con ese username.");
        var user = new AppUser { Username = username, CompanyName = request.CompanyName.Trim(), Ruc = ruc };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        user.Emails.Add(new UserEmail { Email = email, IsPrimary = true });
        user.UserRoles.Add(new UserRole { Role = role });
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    public async Task<AdminUserResponse?> AssignRoleAsync(Guid id, AssignRoleRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.Include(item => item.Emails).Include(item => item.UserRoles).ThenInclude(item => item.Role).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (user is null) return null;
        var role = await FindRoleAsync(request.Role, cancellationToken);
        user.UserRoles.Clear();
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, Role = role });
        user.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    public async Task<AdminUserResponse?> SetStatusAsync(Guid id, UpdateUserStatusRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.Include(item => item.Emails).Include(item => item.UserRoles).ThenInclude(item => item.Role).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (user is null) return null;
        user.IsActive = request.IsActive;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    private async Task<Role> FindRoleAsync(string value, CancellationToken cancellationToken)
    {
        var role = await db.Roles.SingleOrDefaultAsync(item => item.Code == value || item.Name == value, cancellationToken);
        return role ?? throw new InvalidOperationException("El rol indicado no es válido.");
    }

    private static AdminUserResponse ToResponse(AppUser user)
    {
        var email = user.Emails.FirstOrDefault(item => item.IsPrimary && item.IsActive)?.Email ?? user.Emails.FirstOrDefault(item => item.IsActive)?.Email ?? string.Empty;
        var roles = user.UserRoles.Where(item => item.Role.IsActive).Select(item => item.Role.Name).OrderBy(name => name).ToArray();
        return new AdminUserResponse(user.Id, user.Username, email, user.CompanyName, user.Ruc ?? string.Empty, roles.FirstOrDefault() ?? string.Empty, roles, user.IsActive, user.CreatedAtUtc);
    }
}
