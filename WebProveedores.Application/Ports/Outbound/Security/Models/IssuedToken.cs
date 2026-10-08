namespace WebProveedores.Application.Ports.Outbound.Security.Models;

public sealed record IssuedToken(string Value, DateTime ExpiresAtUtc);
