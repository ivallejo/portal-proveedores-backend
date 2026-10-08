using System.ComponentModel.DataAnnotations;
using WebProveedores.Application.Contracts.Documents.Commands;
using WebProveedores.Domain.Documents;

namespace WebProveedores.Api.Controllers;

public sealed class RegisterDocumentForm
{
    [Required] public DocumentEntryType EntryType { get; init; }
    [Required, MaxLength(20)] public string CompanyCode { get; init; } = string.Empty;
    public bool IsPettyCash { get; init; }
    public OrderType? OrderType { get; init; }
    [MaxLength(20)] public string? OrderNumber { get; init; }
    public Guid? ApproverId { get; init; }
    public IFormFile? Xml { get; init; }
    public IFormFile? Pdf { get; init; }
    public IFormFile? Cdr { get; init; }
    public List<IFormFile>? Extras { get; init; }
}

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
