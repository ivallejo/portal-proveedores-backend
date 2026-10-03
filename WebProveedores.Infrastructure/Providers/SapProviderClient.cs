using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace WebProveedores.Infrastructure.Providers;

public sealed class SapProviderClient(HttpClient httpClient, IConfiguration configuration)
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

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        var providers = await response.Content.ReadFromJsonAsync<List<SapProviderRecord>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);

        return providers?.FirstOrDefault(provider => !string.IsNullOrWhiteSpace(provider.Correo));
    }
}

public sealed record SapProviderRecord(
    string? Stcd1,
    string? Name1,
    string? Name2,
    string? Adrnr,
    string? Correo)
{
    public string CompanyName => string.Join(" ", new[] { Name1, Name2 }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
}
