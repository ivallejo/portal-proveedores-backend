using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Application.Contracts.Documents.Requests;

public sealed class RejectDocumentRequest
{
    [Required, MaxLength(1000)] public string Reason { get; init; } = string.Empty;
}
