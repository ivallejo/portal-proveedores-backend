using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using WebProveedores.Application.Abstractions;
using WebProveedores.Application.Abstractions.Providers;

namespace WebProveedores.Infrastructure.Providers;

public sealed class SapProviderClient(HttpClient httpClient, IConfiguration configuration) : IProviderDirectory
{
    public async Task<SapProviderRecord?> FindByRucAsync(string ruc, CancellationToken cancellationToken)
    {
        var baseUrl = configuration["Sap:BaseUrl"]?.TrimEnd('/');
        var sapClient = configuration["Sap:Client"] ?? "200";
        var basicToken = configuration["Sap:BasicToken"];

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(basicToken))
            throw new InvalidOperationException("La integración con SAP no está configurada.");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{baseUrl}/sap/bc/zconsruc?sap-client={Uri.EscapeDataString(sapClient)}&ruc={Uri.EscapeDataString(ruc)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicToken);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
            var providers = await response.Content.ReadFromJsonAsync<List<SapProviderRecord>>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);

            return providers?.FirstOrDefault(provider => !string.IsNullOrWhiteSpace(provider.Correo));
        }
        // Red caída, VPN desconectada, tiempo agotado o circuito abierto: SAP no está disponible, no es un error del usuario.
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            throw new ServiceUnavailableException("No pudimos consultar SAP en este momento. Intenta nuevamente en unos minutos.", exception);
        }
    }
}
