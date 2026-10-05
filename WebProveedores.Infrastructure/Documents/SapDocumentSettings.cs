using Microsoft.Extensions.Configuration;

namespace WebProveedores.Infrastructure.Documents;

/// <summary>
/// Implementación de los servicios SAP 01 (orden) y 02 (SUNAT y duplicidad), <c>Sap:DocumentServices</c>.
/// Hoy solo existe el simulador: fuera de desarrollo debe aceptarse de forma explícita, para que nunca
/// llegue a producción sin que alguien lo decida.
/// </summary>
public sealed record SapDocumentSettings(string Mode)
{
    public const string Simulated = "Simulated";

    public static SapDocumentSettings Resolve(IConfiguration configuration, bool isProduction)
    {
        var configured = configuration["Sap:DocumentServices"]?.Trim();
        if (string.IsNullOrEmpty(configured))
        {
            if (isProduction)
                throw new InvalidOperationException(
                    "Los servicios SAP 01/02 aún están simulados. Para usarlos así en producción define Sap:DocumentServices=Simulated.");
            return new SapDocumentSettings(Simulated);
        }
        return string.Equals(configured, Simulated, StringComparison.OrdinalIgnoreCase)
            ? new SapDocumentSettings(Simulated)
            : throw new InvalidOperationException($"Sap:DocumentServices «{configured}» no es válido. Por ahora solo existe «{Simulated}».");
    }
}
