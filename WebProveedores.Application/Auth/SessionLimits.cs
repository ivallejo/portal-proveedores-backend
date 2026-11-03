namespace WebProveedores.Application.Auth;

/// <summary>La sesión se renueva mientras haya actividad, pero no más allá de este tope desde el ingreso.</summary>
internal static class SessionLimits
{
    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(12);
}
