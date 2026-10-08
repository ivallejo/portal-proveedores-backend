using WebProveedores.Application.Documents.Responses;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Organization;

namespace WebProveedores.Application.Documents;

/// <summary>Entidades del dominio → respuestas de la API.</summary>
internal static class DocumentMapper
{
    public static CompanyResponse ToResponse(Company company) => new(company.Code, company.Name, company.Ruc, company.BillingEmail);

    public static DocumentSummaryResponse ToSummary(SupplierDocument document) => new(
        document.Id, document.Number, document.EntryType, document.DocumentType, document.ProviderRuc, document.ProviderName,
        document.Currency, document.Amount, document.Status, document.IsPettyCash, document.IssuedAt, document.RegisteredAtUtc, document.ApproverName, document.OrderNumber);

    public static DocumentDetailResponse ToDetail(SupplierDocument document) => new(
        document.Id, document.Number, document.EntryType, document.DocumentType, document.ProviderRuc, document.ProviderName, document.ProviderEmail,
        document.Currency, document.Subtotal, document.Igv, document.Amount, document.Concept, document.IssuedAt, document.RegisteredAtUtc,
        document.RegisteredByName, ToResponse(document.Company), document.Status, document.IsPettyCash, document.RejectedBy, document.AreaName, document.ApproverName,
        document.ApproverEmail, document.ApprovedAtUtc, document.ApprovalReferenceType, document.ApprovalReference, document.OrderType,
        document.OrderNumber, document.OrderBalance, document.OrderDescription, document.Validation,
        document.Items.OrderBy(item => item.LineNumber).Select(item => new DocumentItemResponse(item.Description, item.Quantity, item.UnitPrice, item.Amount)).ToArray(),
        document.Attachments.OrderBy(item => item.Kind).ThenBy(item => item.FileName).Select(item => new AttachmentResponse(item.Id, item.Kind, item.FileName, item.SizeBytes)).ToArray(),
        document.Events.OrderBy(item => item.Sequence).Select(item => new DocumentEventResponse(item.Title, item.Actor, item.Kind, item.OccurredAtUtc, item.Note)).ToArray());
}
