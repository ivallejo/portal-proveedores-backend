namespace WebProveedores.Application.Common.Settings;

/// <summary>Dirección pública del frontend, para los enlaces de los correos.</summary>
public sealed record PortalSettings(string FrontendBaseUrl)
{
    public string Link(string query) => $"{FrontendBaseUrl.TrimEnd('/')}/?{query}";
}
