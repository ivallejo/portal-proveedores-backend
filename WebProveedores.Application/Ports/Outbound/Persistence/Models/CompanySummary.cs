using WebProveedores.Domain.Organization;

namespace WebProveedores.Application.Ports.Outbound.Persistence.Models;

/// <summary>Sociedad con cuántas áreas tiene y cuántos usuarios trabajan con ella.</summary>
public sealed record CompanySummary(Company Company, int AreaCount, int UserCount);
