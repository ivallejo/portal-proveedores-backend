using System.ComponentModel.DataAnnotations;
using WebProveedores.Application.Contracts.Documents.Commands;
using WebProveedores.Domain.Documents;

namespace WebProveedores.Api.Contracts.Documents;

public sealed class RegisterSpecialDocumentForm
{
    [Required, MaxLength(20)] public string CompanyCode { get; init; } = string.Empty;
    [Required] public SpecialDocumentType DocumentType { get; init; }
    [Required, RegularExpression(@"^\d{11}$")] public string ProviderRuc { get; init; } = string.Empty;
    [Required] public DateOnly IssuedAt { get; init; }
    [Required, MaxLength(30)] public string Number { get; init; } = string.Empty;
    [Range(0.01, 999_999_999)] public decimal Amount { get; init; }
    [Required] public Currency Currency { get; init; }
    public IFormFile? Pdf { get; init; }
}
