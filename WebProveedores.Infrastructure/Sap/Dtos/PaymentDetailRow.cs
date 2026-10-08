using System.Text.Json.Serialization;

namespace WebProveedores.Infrastructure.Sap.Dtos;

internal sealed class PaymentDetailRow
{
    [JsonPropertyName("xblnr")] public string Xblnr { get; init; } = string.Empty;
    [JsonPropertyName("bldat")] public string? Bldat { get; init; }
    [JsonPropertyName("wrbtr")] public string? Wrbtr { get; init; }
    [JsonPropertyName("qbshb")] public string? Qbshb { get; init; }
    [JsonPropertyName("detra")] public string? Detra { get; init; }
    [JsonPropertyName("rwbtr")] public string? Rwbtr { get; init; }
    [JsonPropertyName("comp_reten")] public string? CompReten { get; init; }
    [JsonPropertyName("info_detracc")] public string? InfoDetracc { get; init; }
    [JsonPropertyName("doc_ret")] public RetentionRow? DocRet { get; init; }
    [JsonPropertyName("doc_det")] public DetractionRow? DocDet { get; init; }
}
