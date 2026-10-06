using Microsoft.Extensions.Configuration;

namespace WebProveedores.Infrastructure.Providers;

/// <summary>Conexión a los servicios ICF de SAP (<c>Sap:BaseUrl</c>, <c>Sap:Client</c>, <c>Sap:BasicToken</c>).</summary>
public sealed record SapSettings(string BaseUrl, string Client, string BasicToken)
{
    public static SapSettings From(IConfiguration configuration)
    {
        var baseUrl = configuration["Sap:BaseUrl"]?.TrimEnd('/');
        var token = configuration["Sap:BasicToken"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("La integración con SAP no está configurada (Sap:BaseUrl y Sap:BasicToken).");
        return new SapSettings(baseUrl, configuration["Sap:Client"] ?? "200", token);
    }

    public string Url(string service, string query) => $"{BaseUrl}/sap/bc/{service}?sap-client={Uri.EscapeDataString(Client)}&{query}";
}
