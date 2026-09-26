using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebProveedores.Application.Admin;
using WebProveedores.Domain.Entities;
using WebProveedores.Infrastructure.Persistence;

namespace WebProveedores.Infrastructure.Admin;

public sealed class AdminUserService(AppDbContext db) : IAdminUserService
{
    private static readonly string[] AllowedRoles = ["Proveedor", "Área Usuaria", "CxP", "Administrador"];
    private readonly PasswordHasher<AppUser> passwordHasher = new();

    public async Task<IReadOnlyList<AdminUserResponse>> ListAsync(CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking().OrderBy(user => user.CompanyName).Select(ToResponseExpression()).ToListAsync(cancellationToken);

    public async Task<AdminUserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var role = NormalizeRole(request.Role);
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(user => user.Email == email || user.Ruc == request.Ruc.Trim(), cancellationToken)) throw new InvalidOperationException("Ya existe un usuario con ese correo o RUC.");
        var user = new AppUser { Email = email, CompanyName = request.CompanyName.Trim(), Ruc = request.Ruc.Trim(), Role = role };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    public Task<AdminUserResponse?> AssignRoleAsync(Guid id, AssignRoleRequest request, CancellationToken cancellationToken) => UpdateAsync(id, user => user.Role = NormalizeRole(request.Role), cancellationToken);
    public Task<AdminUserResponse?> SetStatusAsync(Guid id, UpdateUserStatusRequest request, CancellationToken cancellationToken) => UpdateAsync(id, user => user.IsActive = request.IsActive, cancellationToken);

    private async Task<AdminUserResponse?> UpdateAsync(Guid id, Action<AppUser> update, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (user is null) return null;
        update(user); await db.SaveChangesAsync(cancellationToken); return ToResponse(user);
    }

    private static string NormalizeRole(string role) => AllowedRoles.FirstOrDefault(item => item.Equals(role.Trim(), StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException("El rol indicado no es válido.");
    private static AdminUserResponse ToResponse(AppUser user) => new(user.Id, user.Email, user.CompanyName, user.Ruc, user.Role, user.IsActive, user.CreatedAtUtc);
    private static System.Linq.Expressions.Expression<Func<AppUser, AdminUserResponse>> ToResponseExpression() => user => new AdminUserResponse(user.Id, user.Email, user.CompanyName, user.Ruc, user.Role, user.IsActive, user.CreatedAtUtc);
}
