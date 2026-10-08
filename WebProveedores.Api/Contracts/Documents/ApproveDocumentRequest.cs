using System.ComponentModel.DataAnnotations;
using WebProveedores.Domain.Documents;

namespace WebProveedores.Api.Contracts.Documents;

public sealed class ApproveDocumentRequest
{
    [Required] public ApprovalReferenceType ReferenceType { get; init; }
    [Required, MaxLength(30)] public string Reference { get; init; } = string.Empty;
}
