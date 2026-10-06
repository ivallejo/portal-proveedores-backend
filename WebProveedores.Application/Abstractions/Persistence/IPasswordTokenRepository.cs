using WebProveedores.Domain.Entities;

namespace WebProveedores.Application.Abstractions.Persistence;

/// <summary>Tokens de un solo uso para activar la cuenta o recuperar la contraseña.</summary>
public interface IPasswordTokenRepository
{
    /// <summary>Token vigente: no usado, no reemplazado y no vencido (incluye al usuario y sus correos).</summary>
    Task<PasswordResetToken?> FindValidAsync(string tokenHash, PasswordTokenPurpose purpose, CancellationToken cancellationToken);
    /// <summary>Tokens del usuario, del más nuevo al más antiguo (con seguimiento).</summary>
    Task<IReadOnlyList<PasswordResetToken>> ListForUserAsync(Guid userId, CancellationToken cancellationToken);
    void Add(PasswordResetToken token);
}
