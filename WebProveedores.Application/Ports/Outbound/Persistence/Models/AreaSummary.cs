using WebProveedores.Domain.Organization;

namespace WebProveedores.Application.Ports.Outbound.Persistence.Models;

/// <summary>Área (con su sociedad) y cuántos usuarios pertenecen a ella.</summary>
public sealed record AreaSummary(Area Area, int UserCount);
