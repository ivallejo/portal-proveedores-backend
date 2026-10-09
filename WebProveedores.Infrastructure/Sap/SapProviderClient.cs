using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Application.Ports.Outbound.Sap;
using WebProveedores.Application.Ports.Outbound.Sap.Models;

namespace WebProveedores.Infrastructure.Sap;

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

            // Cuando el RUC no existe SAP responde 200 con una fila ficticia («EL RUC: … No existe»): equivale a no encontrado.
            var found = providers?.Where(provider => !IsNotFoundPlaceholder(provider)).ToList() ?? [];
            // Se prefiere el registro con correo; sin ninguno se devuelve igual, para avisar que falta el correo.
            return found.FirstOrDefault(provider => !string.IsNullOrWhiteSpace(provider.Correo)) ?? found.FirstOrDefault();
        }
        // Red caída, VPN desconectada, tiempo agotado o circuito abierto: SAP no está disponible, no es un error del usuario.
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            throw new ServiceUnavailableException("No pudimos consultar SAP en este momento. Intenta nuevamente en unos minutos.", exception);
        }
    }

    /// <summary>
    /// Prefijo del RUC ficticio («9999999991») con el que SAP arma la fila «EL RUC: … No existe». Ningún RUC real
    /// empieza con 9 (los válidos empiezan con 10, 15, 17 o 20).
    /// </summary>
    private const string NotFoundRucPrefix = "99999";

    internal static bool IsNotFoundPlaceholder(SapProviderRecord provider) =>
        provider.Stcd1?.Trim().StartsWith(NotFoundRucPrefix, StringComparison.Ordinal) == true;
}
