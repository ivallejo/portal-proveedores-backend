using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Api.Contracts.Documents;

public sealed class RejectDocumentRequest
{
    [Required, MaxLength(1000)] public string Reason { get; init; } = string.Empty;
}
