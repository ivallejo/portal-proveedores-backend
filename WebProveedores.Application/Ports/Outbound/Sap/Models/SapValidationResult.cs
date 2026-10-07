namespace WebProveedores.Application.Ports.Outbound.Sap.Models;

public sealed record SapValidationResult(bool IsValid, string? Message)
{
    public static SapValidationResult Valid { get; } = new(true, null);
}
