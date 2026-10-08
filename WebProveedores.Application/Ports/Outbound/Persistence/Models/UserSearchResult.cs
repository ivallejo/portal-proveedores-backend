using WebProveedores.Domain.Identity;

namespace WebProveedores.Application.Ports.Outbound.Persistence.Models;

public sealed record UserSearchResult(IReadOnlyList<AppUser> Items, int Total);
