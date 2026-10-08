namespace WebProveedores.Api.Security;

public static class RateLimitPolicies
{
    /// <summary>Intentos de inicio de sesión por IP.</summary>
    public const string Login = "login";

    /// <summary>Consultas de RUC, activación y recuperación de contraseña por IP.</summary>
    public const string Sensitive = "sensitive";
}
