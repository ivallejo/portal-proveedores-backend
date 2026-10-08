using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Application.Contracts.Documents.Requests;

public sealed class ObserveDocumentRequest
{
    [Required, MaxLength(1000)] public string Reason { get; init; } = string.Empty;
    [Required, EmailAddress, MaxLength(320)] public string Email { get; init; } = string.Empty;
}
