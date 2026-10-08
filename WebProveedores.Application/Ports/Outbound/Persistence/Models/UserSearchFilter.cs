using WebProveedores.Domain.Identity;

namespace WebProveedores.Application.Ports.Outbound.Persistence.Models;

/// <summary><paramref name="Status"/>: estado de la cuenta en <paramref name="Now"/> (inactiva, bloqueada o activa).</summary>
public sealed record UserSearchFilter(string? Search, string? RoleCode, UserStatus? Status, DateTime Now);
