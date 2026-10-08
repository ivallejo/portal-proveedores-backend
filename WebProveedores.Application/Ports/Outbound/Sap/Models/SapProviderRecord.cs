namespace WebProveedores.Application.Ports.Outbound.Sap.Models;

public sealed record SapProviderRecord(
    string? Stcd1,
    string? Name1,
    string? Name2,
    string? Adrnr,
    string? Correo)
{
    public string CompanyName => string.Join(" ", new[] { Name1, Name2 }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
}
