namespace WebProveedores.Application.Common.Exceptions;

/// <summary>La cuenta está bloqueada temporalmente por demasiados intentos fallidos.</summary>
public sealed class AccountLockedException(TimeSpan retryAfter) : Exception("Demasiados intentos fallidos. Espera unos minutos antes de volver a intentarlo.")
{
    public TimeSpan RetryAfter { get; } = retryAfter;
}
