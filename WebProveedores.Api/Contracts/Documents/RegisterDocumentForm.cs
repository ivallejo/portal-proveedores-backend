using System.ComponentModel.DataAnnotations;
using WebProveedores.Domain.Documents;

namespace WebProveedores.Api.Contracts.Documents;

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
