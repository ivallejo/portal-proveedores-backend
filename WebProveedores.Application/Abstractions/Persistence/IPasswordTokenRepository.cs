using WebProveedores.Domain.Entities;

namespace WebProveedores.Application.Abstractions.Persistence;

/// <summary>Tokens de un solo uso para activar la cuenta o recuperar la contraseña.</summary>
public interface IPasswordTokenRepository
{
    Task<PasswordResetToken?> FindValidAsync(string ruc, string tokenHash, PasswordTokenPurpose purpose, CancellationToken cancellationToken);
    void Add(PasswordResetToken token);
}
