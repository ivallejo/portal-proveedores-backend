using System.Text.Json.Serialization;

namespace WebProveedores.Infrastructure.Sap.Dtos;

internal sealed class DetractionRow
{
    [JsonPropertyName("const_detrac")] public string? ConstDetrac { get; init; }
    [JsonPropertyName("porc_det")] public string? PorcDet { get; init; }
}
