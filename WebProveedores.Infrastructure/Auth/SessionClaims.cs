namespace WebProveedores.Infrastructure.Auth;

/// <summary>Claims propios del token de sesión.</summary>
public static class SessionClaims
{
    /// <summary>La contraseña es temporal: la sesión solo sirve para cambiarla.</summary>
    public const string PasswordChangeOnly = "pwd_change";
    public const string Username = "username";
    public const string Ruc = "ruc";
}
