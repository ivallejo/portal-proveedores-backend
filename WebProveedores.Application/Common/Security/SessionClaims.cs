namespace WebProveedores.Application.Common.Security;

/// <summary>Claims propios del token de sesión.</summary>
public static class SessionClaims
{
    /// <summary>La contraseña es temporal: la sesión solo sirve para cambiarla.</summary>
    public const string PasswordChangeOnly = "pwd_change";
    public const string Username = "username";
    public const string Ruc = "ruc";
    /// <summary>Inicio de la sesión (segundos Unix): se conserva al renovar el token, para el tope de duración.</summary>
    public const string StartedAt = "session_start";
}
