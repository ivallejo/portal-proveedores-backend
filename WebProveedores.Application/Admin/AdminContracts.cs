using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Application.Admin;

public sealed class CreateUserRequest
{
    [MaxLength(80)] public string Username { get; init; } = string.Empty;
    [Required, EmailAddress] public string Email { get; init; } = string.Empty;
    [Required, MaxLength(200)] public string CompanyName { get; init; } = string.Empty;
    [MaxLength(20)] public string Ruc { get; init; } = string.Empty;
    [Required, MinLength(8)] public string Password { get; init; } = string.Empty;
    [Required] public string Role { get; init; } = string.Empty;
    /// <summary>Códigos de sociedad; vacío asigna todas las sociedades activas.</summary>
    public IReadOnlyList<string> CompanyCodes { get; init; } = [];
}

public sealed class AssignCompaniesRequest
{
    [Required, MinLength(1)] public IReadOnlyList<string> CompanyCodes { get; init; } = [];
}

public sealed class AssignRoleRequest
{
    [Required] public string Role { get; init; } = string.Empty;
}
public sealed record UpdateUserStatusRequest(bool IsActive);
public sealed record AdminUserResponse(Guid Id, string Username, string Email, string CompanyName, string Ruc, string Role, IReadOnlyList<string> Roles, IReadOnlyList<string> CompanyCodes, bool IsActive, DateTime CreatedAtUtc);

public interface IAdminUserService
{
    Task<IReadOnlyList<AdminUserResponse>> ListAsync(CancellationToken cancellationToken);
    Task<AdminUserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<AdminUserResponse?> AssignRoleAsync(Guid id, AssignRoleRequest request, CancellationToken cancellationToken);
    Task<AdminUserResponse?> AssignCompaniesAsync(Guid id, AssignCompaniesRequest request, CancellationToken cancellationToken);
    Task<AdminUserResponse?> SetStatusAsync(Guid id, UpdateUserStatusRequest request, CancellationToken cancellationToken);
}
