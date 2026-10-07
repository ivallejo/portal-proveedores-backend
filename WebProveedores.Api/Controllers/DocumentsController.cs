using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebProveedores.Api.Infrastructure;
using WebProveedores.Application.Documents;
using WebProveedores.Application.Ports.Inbound.Documents;
using WebProveedores.Application.Ports.Outbound.Persistence.Models;
using WebProveedores.Domain.Documents;

namespace WebProveedores.Api.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize]
public sealed class DocumentsController(
    IDocumentRegistrationService registration,
    IDocumentQueryService queries,
    IDocumentApprovalService approvals,
    IDocumentAccountingService accounting,
    IDocumentCatalogService catalog,
    ICurrentUser currentUser) : ControllerBase
{
    private const long MaxRequestBytes = 40 * 1024 * 1024;

    [HttpPost("orders/validate")]
    [Authorize(Policy = Policies.DocumentsRegister)]
    public async Task<ActionResult<OrderValidationResponse>> ValidateOrder(ValidateOrderRequest request, CancellationToken cancellationToken) =>
        await catalog.ValidateOrderAsync(UserId, request, cancellationToken) is { } order
            ? Ok(order)
            : UnprocessableEntity(new { message = "SAP no encontró la orden para la sociedad seleccionada o ya no tiene saldo por facturar." });

    /// <summary>Registra un comprobante electrónico con o sin orden de compra.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.DocumentsRegister)]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<ActionResult<DocumentDetailResponse>> Register([FromForm] RegisterDocumentForm form, CancellationToken cancellationToken)
    {
        var command = new RegisterElectronicDocumentCommand(
            form.EntryType, form.CompanyCode, form.IsPettyCash, form.OrderType, form.OrderNumber, form.ApproverId,
            ToUpload(form.Xml), ToUpload(form.Pdf), ToUpload(form.Cdr),
            (form.Extras ?? []).Select(ToUpload).OfType<UploadedFile>().ToArray());
        var created = await registration.RegisterAsync(UserId, command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Registra un documento especial (personal interno).</summary>
    [HttpPost("special")]
    [Authorize(Policy = Policies.DocumentsRegister)]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<ActionResult<DocumentDetailResponse>> RegisterSpecial([FromForm] RegisterSpecialDocumentForm form, CancellationToken cancellationToken)
    {
        var command = new RegisterSpecialDocumentCommand(
            form.CompanyCode, form.DocumentType, form.ProviderRuc, form.IssuedAt, form.Number, form.Amount, form.Currency, ToUpload(form.Pdf));
        var created = await registration.RegisterSpecialAsync(UserId, command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Bandejas: <c>approvals</c>, <c>accounting</c> o <c>mine</c>.</summary>
    [HttpGet]
    public async Task<ActionResult<DocumentPageResponse>> Search(
        [FromQuery, Required] DocumentInbox inbox,
        [FromQuery] string? ruc,
        [FromQuery] DocumentStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        Ok(await queries.SearchAsync(UserId, inbox, ruc, status, page, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentDetailResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await queries.GetAsync(UserId, id, cancellationToken));

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DownloadAttachment(Guid id, Guid attachmentId, CancellationToken cancellationToken)
    {
        var file = await queries.OpenAttachmentAsync(UserId, id, attachmentId, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = Policies.DocumentsApprove)]
    public async Task<ActionResult<DocumentDetailResponse>> Approve(Guid id, ApproveDocumentRequest request, CancellationToken cancellationToken) =>
        Ok(await approvals.ApproveAsync(UserId, id, request, cancellationToken));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = Policies.DocumentsApprove)]
    public async Task<ActionResult<DocumentDetailResponse>> Reject(Guid id, RejectDocumentRequest request, CancellationToken cancellationToken) =>
        Ok(await approvals.RejectAsync(UserId, id, request, cancellationToken));

    [HttpPost("{id:guid}/reassign")]
    [Authorize(Policy = Policies.DocumentsApprove)]
    public async Task<ActionResult<DocumentDetailResponse>> Reassign(Guid id, ReassignDocumentRequest request, CancellationToken cancellationToken) =>
        Ok(await approvals.ReassignAsync(UserId, id, request, cancellationToken));

    [HttpPost("{id:guid}/accounting/reject")]
    [Authorize(Policy = Policies.DocumentsAccount)]
    public async Task<ActionResult<DocumentDetailResponse>> RejectInAccounting(Guid id, RejectDocumentRequest request, CancellationToken cancellationToken) =>
        Ok(await accounting.RejectAsync(UserId, id, request, cancellationToken));

    [HttpPost("{id:guid}/accounting/observe")]
    [Authorize(Policy = Policies.DocumentsAccount)]
    public async Task<ActionResult<DocumentDetailResponse>> Observe(Guid id, ObserveDocumentRequest request, CancellationToken cancellationToken) =>
        Ok(await accounting.ObserveAsync(UserId, id, request, cancellationToken));

    private Guid UserId => currentUser.Id;

    private static UploadedFile? ToUpload(IFormFile? file) =>
        file is null ? null : new UploadedFile(file.FileName, file.ContentType, file.Length, file.OpenReadStream());
}
