using WebProveedores.Domain.Access;

namespace WebProveedores.Application.Ports.Outbound.Persistence.Models;

public sealed record RoleSummary(Role Role, int UserCount);
