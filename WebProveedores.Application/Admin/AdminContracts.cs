using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Application.Admin;

public sealed record CreateUserRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required, MaxLength(200)] string CompanyName,
    [property: Required, MaxLength(20)] string Ruc,
    [property: Required, MinLength(8)] string Password,
    [property: Required] string Role);
public sealed record AssignRoleRequest([property: Required] string Role);
public sealed record UpdateUserStatusRequest(bool IsActive);
public sealed record AdminUserResponse(Guid Id, string Email, string CompanyName, string Ruc, string Role, bool IsActive, DateTime CreatedAtUtc);

public interface IAdminUserService
{
    Task<IReadOnlyList<AdminUserResponse>> ListAsync(CancellationToken cancellationToken);
    Task<AdminUserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<AdminUserResponse?> AssignRoleAsync(Guid id, AssignRoleRequest request, CancellationToken cancellationToken);
    Task<AdminUserResponse?> SetStatusAsync(Guid id, UpdateUserStatusRequest request, CancellationToken cancellationToken);
}
