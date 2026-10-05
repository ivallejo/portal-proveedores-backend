using WebProveedores.Application.Abstractions.Auth;
using WebProveedores.Application.Abstractions.Persistence;

namespace WebProveedores.Application.Auth;

/// <summary>Inicio de sesión con bloqueo temporal por intentos fallidos, y datos del usuario en sesión.</summary>
public sealed class LoginService(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher hasher,
    ITokenIssuer tokens,
    LoginLockoutSettings lockout,
    TimeProvider clock) : ILoginService
{
    // Hash de relleno: se verifica igual cuando la cuenta no existe, para no delatarlo por el tiempo de respuesta.
    private static string? dummyHash;

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var identifier = request.Identifier.Trim();
        var user = await users.FindForLoginAsync(identifier, identifier.ToLowerInvariant(), cancellationToken);
        if (user is null || !user.IsActive)
        {
            dummyHash ??= hasher.Hash(Guid.CreateVersion7().ToString("N"));
            hasher.Verify(dummyHash, request.Password);
            return null;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (user.IsLockedOut(now))
            throw new AccountLockedException(user.LockoutUntilUtc!.Value - now);

        if (!hasher.Verify(user.PasswordHash, request.Password))
        {
            user.RecordFailedLogin(lockout.MaxFailedLogins, lockout.LockoutMinutes, now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return null;
        }

        if (user.ClearFailedLogins()) await unitOfWork.SaveChangesAsync(cancellationToken);
        return tokens.StartSession(user);
    }

    public async Task<UserResponse?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await users.FindByIdAsync(userId, cancellationToken) is { } user ? AuthSupport.ToResponse(user) : null;
}
