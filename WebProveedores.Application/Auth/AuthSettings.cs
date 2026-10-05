namespace WebProveedores.Application.Auth;

/// <summary>Dirección pública del frontend, para los enlaces de los correos.</summary>
public sealed record PortalSettings(string FrontendBaseUrl)
{
    public string Link(string query) => $"{FrontendBaseUrl.TrimEnd('/')}/?{query}";
}

/// <summary>Bloqueo temporal de la cuenta tras varios intentos fallidos.</summary>
public sealed record LoginLockoutSettings(int MaxFailedLogins, int LockoutMinutes);
