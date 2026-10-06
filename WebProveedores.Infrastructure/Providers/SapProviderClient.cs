using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using WebProveedores.Application.Abstractions;
using WebProveedores.Application.Abstractions.Providers;

namespace WebProveedores.Infrastructure.Providers;

public sealed class SapProviderClient(HttpClient httpClient, SapSettings settings) : IProviderDirectory
{
    public async Task<SapProviderRecord?> FindByRucAsync(string ruc, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, settings.Url("zconsruc", $"ruc={Uri.EscapeDataString(ruc)}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", settings.BasicToken);

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
