using System.Text.Json.Serialization;

namespace WebProveedores.Infrastructure.Sap.Dtos;

internal sealed class RetentionRow
{
    [JsonPropertyName("serie")] public string? Serie { get; init; }
    [JsonPropertyName("numero")] public string? Numero { get; init; }
}
