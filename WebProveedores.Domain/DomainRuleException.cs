namespace WebProveedores.Domain;

/// <summary>Una regla del dominio impide la operación (por ejemplo, aprobar un documento que ya no está pendiente).</summary>
public sealed class DomainRuleException(string message) : Exception(message);
