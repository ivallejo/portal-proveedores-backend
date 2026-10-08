using System.Text.Json.Serialization;

namespace WebProveedores.Infrastructure.Sap.Dtos;

internal sealed class PaymentRow
{
    [JsonPropertyName("laufd")] public string? Laufd { get; init; }
    [JsonPropertyName("opago")] public string Opago { get; init; } = string.Empty;
    [JsonPropertyName("stcd1")] public string Stcd1 { get; init; } = string.Empty;
    [JsonPropertyName("name1")] public string? Name1 { get; init; }
    [JsonPropertyName("name2")] public string? Name2 { get; init; }
    [JsonPropertyName("zbukr")] public string Zbukr { get; init; } = string.Empty;
    [JsonPropertyName("vblnr")] public string Vblnr { get; init; } = string.Empty;
    [JsonPropertyName("waers")] public string Waers { get; init; } = string.Empty;
    [JsonPropertyName("zaldt")] public string? Zaldt { get; init; }
    [JsonPropertyName("rzawe")] public string Rzawe { get; init; } = string.Empty;
    [JsonPropertyName("banka")] public string? Banka { get; init; }
    [JsonPropertyName("ubknt")] public string? Ubknt { get; init; }
    [JsonPropertyName("rwbtr")] public string? Rwbtr { get; init; }
    [JsonPropertyName("ruc_adqui")] public string? RucAdqui { get; init; }
    [JsonPropertyName("nom_adqui")] public string? NomAdqui { get; init; }
    [JsonPropertyName("detalle")] public List<PaymentDetailRow> Detalle { get; init; } = [];
}
