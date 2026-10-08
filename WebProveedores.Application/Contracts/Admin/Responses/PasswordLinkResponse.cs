namespace WebProveedores.Application.Contracts.Admin.Responses;

/// <summary>Enlace de activación o recuperación enviado al usuario (solo se muestra un fragmento del hash).</summary>
public sealed record PasswordLinkResponse(string Kind, string Fingerprint, DateTime CreatedAtUtc, DateTime ExpiresAtUtc, string Status);
