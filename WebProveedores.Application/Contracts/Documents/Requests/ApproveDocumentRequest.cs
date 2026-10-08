using System.ComponentModel.DataAnnotations;
using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Contracts.Documents.Requests;

public sealed class ApproveDocumentRequest
{
    [Required] public ApprovalReferenceType ReferenceType { get; init; }
    [Required, MaxLength(30)] public string Reference { get; init; } = string.Empty;
}
