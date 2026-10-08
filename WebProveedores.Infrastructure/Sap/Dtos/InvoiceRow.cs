using System.Text.Json.Serialization;

namespace WebProveedores.Infrastructure.Sap.Dtos;

internal sealed class InvoiceRow
{
    [JsonPropertyName("stcd1")] public string Stcd1 { get; init; } = string.Empty;
    [JsonPropertyName("xblnr")] public string Xblnr { get; init; } = string.Empty;
    [JsonPropertyName("rwbtr")] public string? Rwbtr { get; init; }
    [JsonPropertyName("waers")] public string Waers { get; init; } = string.Empty;
    [JsonPropertyName("bldat")] public string? Bldat { get; init; }
    [JsonPropertyName("detra")] public string? Detra { get; init; }
    [JsonPropertyName("reten")] public string? Reten { get; init; }
    [JsonPropertyName("estad")] public string Estad { get; init; } = string.Empty;
    [JsonPropertyName("ruc_adqui")] public string? RucAdqui { get; init; }
}
