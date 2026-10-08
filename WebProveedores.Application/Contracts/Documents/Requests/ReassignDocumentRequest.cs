using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Application.Contracts.Documents.Requests;

public sealed class ReassignDocumentRequest
{
    [Required] public Guid ApproverId { get; init; }
    [Required, MaxLength(1000)] public string Reason { get; init; } = string.Empty;
}
