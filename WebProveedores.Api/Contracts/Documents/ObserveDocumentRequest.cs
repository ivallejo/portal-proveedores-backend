using System.ComponentModel.DataAnnotations;

namespace WebProveedores.Api.Contracts.Documents;

public sealed class ObserveDocumentRequest
{
    [Required, MaxLength(1000)] public string Reason { get; init; } = string.Empty;
    [Required, EmailAddress, MaxLength(320)] public string Email { get; init; } = string.Empty;
}
