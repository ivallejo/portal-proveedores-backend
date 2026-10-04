using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebProveedores.Application.Abstractions.Documents;
using WebProveedores.Application.Documents;
using WebProveedores.Domain.Documents;

namespace WebProveedores.Api.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize]
public sealed class DocumentsController(IDocumentService documents) : ControllerBase
{
    private const long MaxRequestBytes = 40 * 1024 * 1024;

    [HttpPost("orders/validate")]
    [Authorize(Policy = Policies.DocumentsRegister)]
    public async Task<ActionResult<OrderValidationResponse>> ValidateOrder(ValidateOrderRequest request, CancellationToken cancellationToken) =>
        await documents.ValidateOrderAsync(UserId, request, cancellationToken) is { } order
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
            form.EntryType, form.CompanyCode, form.OrderType, form.OrderNumber, form.ApproverId,
            ToUpload(form.Xml), ToUpload(form.Pdf), ToUpload(form.Cdr),
            (form.Extras ?? []).Select(ToUpload).OfType<UploadedFile>().ToArray());
        var created = await documents.RegisterAsync(UserId, command, cancellationToken);
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
        var created = await documents.RegisterSpecialAsync(UserId, command, cancellationToken);
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
        Ok(await documents.SearchAsync(UserId, inbox, ruc, status, page, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentDetailResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await documents.GetAsync(UserId, id, cancellationToken));

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DownloadAttachment(Guid id, Guid attachmentId, CancellationToken cancellationToken)
    {
        var file = await documents.OpenAttachmentAsync(UserId, id, attachmentId, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = Policies.DocumentsApprove)]
    public async Task<ActionResult<DocumentDetailResponse>> Approve(Guid id, ApproveDocumentRequest request, CancellationToken cancellationToken) =>
        Ok(await documents.ApproveAsync(UserId, id, request, cancellationToken));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = Policies.DocumentsApprove)]
    public async Task<ActionResult<DocumentDetailResponse>> Reject(Guid id, RejectDocumentRequest request, CancellationToken cancellationToken) =>
        Ok(await documents.RejectAsync(UserId, id, RejectionStage.Approver, request, cancellationToken));

    [HttpPost("{id:guid}/reassign")]
    [Authorize(Policy = Policies.DocumentsApprove)]
    public async Task<ActionResult<DocumentDetailResponse>> Reassign(Guid id, ReassignDocumentRequest request, CancellationToken cancellationToken) =>
        Ok(await documents.ReassignAsync(UserId, id, request, cancellationToken));

    [HttpPost("{id:guid}/accounting/reject")]
    [Authorize(Policy = Policies.DocumentsAccount)]
    public async Task<ActionResult<DocumentDetailResponse>> RejectInAccounting(Guid id, RejectDocumentRequest request, CancellationToken cancellationToken) =>
        Ok(await documents.RejectAsync(UserId, id, RejectionStage.Accounting, request, cancellationToken));

    [HttpPost("{id:guid}/accounting/observe")]
    [Authorize(Policy = Policies.DocumentsAccount)]
    public async Task<ActionResult<DocumentDetailResponse>> Observe(Guid id, ObserveDocumentRequest request, CancellationToken cancellationToken) =>
        Ok(await documents.ObserveAsync(UserId, id, request, cancellationToken));

    private Guid UserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
            ? id
            : throw new UnauthorizedAccessException("La sesión no es válida.");

    private static UploadedFile? ToUpload(IFormFile? file) =>
        file is null ? null : new UploadedFile(file.FileName, file.ContentType, file.Length, file.OpenReadStream());
}

[ApiController]
[Route("api/catalog")]
[Authorize]
public sealed class CatalogController(IDocumentService documents) : ControllerBase
{
    [HttpGet("companies")]
    public async Task<ActionResult<IReadOnlyList<CompanyResponse>>> Companies(CancellationToken cancellationToken) =>
        Ok(await documents.ListCompaniesAsync(cancellationToken));

    /// <summary>Áreas con sus aprobadores activos.</summary>
    [HttpGet("areas")]
    public async Task<ActionResult<IReadOnlyList<AreaResponse>>> Areas(CancellationToken cancellationToken) =>
        Ok(await documents.ListAreasAsync(cancellationToken));
}

public sealed class RegisterDocumentForm
{
    [Required] public DocumentEntryType EntryType { get; init; }
    [Required, MaxLength(20)] public string CompanyCode { get; init; } = string.Empty;
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

public static class Policies
{
    public const string UsersManage = "Users.Manage";
    public const string DocumentsRegister = "Documents.Register";
    public const string DocumentsApprove = "Documents.Approve";
    public const string DocumentsAccount = "Documents.Account";
}
