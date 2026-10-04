using System.Globalization;
using Microsoft.Extensions.Logging;
using WebProveedores.Application.Abstractions.Auth;
using WebProveedores.Application.Abstractions.Documents;
using WebProveedores.Application.Abstractions.Persistence;
using WebProveedores.Domain.Documents;
using WebProveedores.Domain.Entities;

namespace WebProveedores.Application.Documents;

public interface IDocumentService
{
    Task<IReadOnlyList<CompanyResponse>> ListCompaniesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AreaResponse>> ListAreasAsync(CancellationToken cancellationToken);
    Task<OrderValidationResponse?> ValidateOrderAsync(Guid userId, ValidateOrderRequest request, CancellationToken cancellationToken);
    Task<DocumentDetailResponse> RegisterAsync(Guid userId, RegisterElectronicDocumentCommand command, CancellationToken cancellationToken);
    Task<DocumentDetailResponse> RegisterSpecialAsync(Guid userId, RegisterSpecialDocumentCommand command, CancellationToken cancellationToken);
    Task<DocumentPageResponse> SearchAsync(Guid userId, DocumentInbox inbox, string? providerRuc, DocumentStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<DocumentDetailResponse> GetAsync(Guid userId, Guid documentId, CancellationToken cancellationToken);
    Task<AttachmentContent> OpenAttachmentAsync(Guid userId, Guid documentId, Guid attachmentId, CancellationToken cancellationToken);
    Task<DocumentDetailResponse> ApproveAsync(Guid userId, Guid documentId, ApproveDocumentRequest request, CancellationToken cancellationToken);
    Task<DocumentDetailResponse> RejectAsync(Guid userId, Guid documentId, RejectionStage stage, RejectDocumentRequest request, CancellationToken cancellationToken);
    Task<DocumentDetailResponse> ReassignAsync(Guid userId, Guid documentId, ReassignDocumentRequest request, CancellationToken cancellationToken);
    Task<DocumentDetailResponse> ObserveAsync(Guid userId, Guid documentId, ObserveDocumentRequest request, CancellationToken cancellationToken);
}

public sealed class DocumentService(
    IDocumentRepository documents,
    IIdentityRepository identities,
    IFileStorage storage,
    ISapDocumentGateway sap,
    IPdfMerger pdfMerger,
    IEmailSender emailSender,
    TimeProvider clock,
    ILogger<DocumentService> logger) : IDocumentService
{
    public const long MaxFileBytes = 5 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<SpecialDocumentType, string> SpecialTypeNames = new Dictionary<SpecialDocumentType, string>
    {
        [SpecialDocumentType.AirTicket] = "Boleto aéreo",
        [SpecialDocumentType.PublicReceipt] = "Recibo público",
        [SpecialDocumentType.NonDomiciled] = "No domiciliado",
        [SpecialDocumentType.CollectionSettlement] = "Liquidación de cobranzas",
    };

    // ——— Catálogos ———

    public async Task<IReadOnlyList<CompanyResponse>> ListCompaniesAsync(CancellationToken cancellationToken) =>
        (await documents.ListCompaniesAsync(cancellationToken)).Select(ToResponse).ToArray();

    public async Task<IReadOnlyList<AreaResponse>> ListAreasAsync(CancellationToken cancellationToken)
    {
        var approvers = await documents.ListApproversAsync(cancellationToken);
        return approvers
            .GroupBy(approver => (approver.AreaId, approver.AreaName))
            .OrderBy(group => group.Key.AreaName)
            .Select(group => new AreaResponse(
                group.Key.AreaId,
                group.Key.AreaName,
                group.OrderBy(item => item.Name).Select(item => new ApproverResponse(item.UserId, item.Name, item.Email)).ToArray()))
            .ToArray();
    }

    // ——— Registro ———

    public async Task<OrderValidationResponse?> ValidateOrderAsync(Guid userId, ValidateOrderRequest request, CancellationToken cancellationToken)
    {
        await LoadActorAsync(userId, cancellationToken);
        var company = await RequireCompanyAsync(request.CompanyCode, cancellationToken);
        var order = await sap.ValidateOrderAsync(company.Code, request.OrderType, request.Number.Trim().ToUpperInvariant(), cancellationToken);
        return order is null ? null : new OrderValidationResponse(order.Number, order.Type, order.Description, order.Balance);
    }

    public async Task<DocumentDetailResponse> RegisterAsync(Guid userId, RegisterElectronicDocumentCommand command, CancellationToken cancellationToken)
    {
        var actor = await LoadActorAsync(userId, cancellationToken);
        if (!actor.CanRegister) throw new UnauthorizedAccessException("No tienes permiso para registrar documentos.");
        if (command.EntryType == DocumentEntryType.Special)
            throw new ArgumentException("Los documentos especiales se registran con su propio formulario.");

        var company = await RequireCompanyAsync(command.CompanyCode, cancellationToken);
        var xmlBytes = await ReadRequiredAsync(command.Xml, "XML del comprobante", [".xml"], cancellationToken);
        var electronic = UblDocumentReader.Read(new MemoryStream(xmlBytes))
            ?? throw new ArgumentException("El XML no es un comprobante electrónico válido (UBL 2.1).");

        if (actor.IsProvider && !actor.IsAdmin && electronic.IssuerRuc != actor.Ruc)
            throw new ArgumentException($"El XML fue emitido por el RUC {electronic.IssuerRuc}. Solo puedes registrar documentos emitidos por tu RUC.");
        if (!string.IsNullOrEmpty(company.Ruc) && !string.IsNullOrEmpty(electronic.ReceiverRuc) && electronic.ReceiverRuc != company.Ruc)
            throw new ArgumentException($"El receptor del XML (RUC {electronic.ReceiverRuc}) no corresponde a la sociedad {company.Name}.");

        if (command.IsPettyCash)
        {
            if (command.EntryType != DocumentEntryType.WithoutPurchaseOrder)
                throw new ArgumentException("Solo los documentos sin orden de compra pueden ser de Caja Chica.");
            if (!actor.IsInternal && !actor.IsAdmin)
                throw new UnauthorizedAccessException("Solo el personal interno puede registrar documentos de Caja Chica.");
        }

        var pdfBytes = await ReadRequiredAsync(command.Pdf, "PDF del comprobante", [".pdf"], cancellationToken);
        byte[]? cdrBytes = null;
        if (electronic.RequiresCdr)
            cdrBytes = await ReadRequiredAsync(command.Cdr, "CDR", [".zip", ".xml"], cancellationToken);
        var extras = new List<(UploadedFile File, byte[] Bytes)>();
        foreach (var extra in command.Extras)
            extras.Add((extra, await ReadRequiredAsync(extra, "archivo de sustento", [".pdf"], cancellationToken)));

        SapOrder? order = null;
        if (command.EntryType == DocumentEntryType.WithPurchaseOrder)
        {
            if (command.OrderType is null || string.IsNullOrWhiteSpace(command.OrderNumber))
                throw new ArgumentException("Indica el tipo y el número de la orden.");
            order = await sap.ValidateOrderAsync(company.Code, command.OrderType.Value, command.OrderNumber.Trim().ToUpperInvariant(), cancellationToken)
                ?? throw new DocumentRejectedException("SAP no encontró la orden para la sociedad seleccionada o ya no tiene saldo por facturar.");
        }

        // Sin OC pasa por aprobación (proveedor o usuario interno) salvo que sea de Caja Chica.
        var needsApproval = command.EntryType == DocumentEntryType.WithoutPurchaseOrder && !command.IsPettyCash;
        ApproverRecord? approver = null;
        if (needsApproval)
        {
            if (command.ApproverId is null) throw new ArgumentException("Selecciona el área y el aprobador del documento.");
            approver = await documents.FindApproverAsync(command.ApproverId.Value, cancellationToken)
                ?? throw new ArgumentException("El aprobador seleccionado no es válido.");
        }

        await EnsureValidInSapAsync(company.Code, electronic.IssuerRuc, electronic.IssuedAt, electronic.Number, electronic.Total, checkSunat: true, cancellationToken);

        var now = clock.GetUtcNow().UtcDateTime;
        var document = new SupplierDocument
        {
            Number = electronic.Number,
            EntryType = command.EntryType,
            DocumentType = electronic.DocumentType,
            ProviderRuc = electronic.IssuerRuc,
            ProviderName = string.IsNullOrWhiteSpace(electronic.IssuerName) ? actor.Name : electronic.IssuerName,
            ProviderEmail = actor.IsProvider ? actor.Email : null,
            Currency = electronic.Currency,
            Subtotal = electronic.Subtotal,
            Igv = electronic.Igv,
            Amount = electronic.Total,
            Concept = string.Join("; ", electronic.Lines.Select(line => line.Description)),
            IssuedAt = electronic.IssuedAt,
            RegisteredAtUtc = now,
            RegisteredById = actor.Id,
            RegisteredByName = actor.RegistrationLabel,
            CompanyId = company.Id,
            Company = company,
            Status = needsApproval ? DocumentStatus.PendingApproval : DocumentStatus.PendingAccounting,
            IsPettyCash = command.IsPettyCash,
            Validation = "Documento validado en SAP y SUNAT",
        };
        if (order is not null)
        {
            document.OrderType = order.Type;
            document.OrderNumber = order.Number;
            document.OrderBalance = order.Balance;
            document.OrderDescription = order.Description;
        }
        if (approver is not null)
        {
            document.AreaId = approver.AreaId;
            document.AreaName = approver.AreaName;
            document.ApproverId = approver.UserId;
            document.ApproverName = approver.Name;
            document.ApproverEmail = approver.Email;
        }
        var lineNumber = 0;
        foreach (var line in electronic.Lines)
            document.Items.Add(new DocumentItem { DocumentId = document.Id, LineNumber = ++lineNumber, Description = line.Description, Quantity = line.Quantity, UnitPrice = line.UnitPrice, Amount = line.Amount });

        var stored = new List<string>();
        try
        {
            await AttachAsync(document, AttachmentKind.Xml, command.Xml!, xmlBytes, stored, cancellationToken);
            await AttachAsync(document, AttachmentKind.Pdf, command.Pdf!, pdfBytes, stored, cancellationToken);
            if (cdrBytes is not null) await AttachAsync(document, AttachmentKind.Cdr, command.Cdr!, cdrBytes, stored, cancellationToken);
            if (extras.Count > 0)
            {
                // Los PDF de sustento se consolidan en un solo archivo.
                var merged = MergeSupport(extras.Select(item => item.Bytes).ToArray());
                var name = $"Anexos_{electronic.Number}.pdf";
                await AttachAsync(document, AttachmentKind.Support, new UploadedFile(name, "application/pdf", merged.Length, Stream.Null), merged, stored, cancellationToken);
            }

            AddRegistrationHistory(document, "Documento validado en SAP y SUNAT", now);
            documents.Add(document);
            await documents.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await DiscardAsync(stored);
            throw;
        }

        if (approver is not null)
        {
            await NotifyAsync(approver.Email, $"Documento por aprobar: {document.Number}",
                DocumentEmailTemplates.PendingApproval(approver.Name, document.Number, document.ProviderName, company.Name, FormatAmount(document)), cancellationToken);
        }
        return ToDetail(document);
    }

    public async Task<DocumentDetailResponse> RegisterSpecialAsync(Guid userId, RegisterSpecialDocumentCommand command, CancellationToken cancellationToken)
    {
        var actor = await LoadActorAsync(userId, cancellationToken);
        if (!actor.IsInternal && !actor.IsAdmin)
            throw new UnauthorizedAccessException("Los documentos especiales solo los registra personal interno.");

        var company = await RequireCompanyAsync(command.CompanyCode, cancellationToken);
        var ruc = command.ProviderRuc.Trim();
        if (ruc.Length != 11 || !ruc.All(char.IsAsciiDigit)) throw new ArgumentException("El RUC del proveedor debe tener 11 dígitos.");
        if (string.IsNullOrWhiteSpace(command.Number)) throw new ArgumentException("Ingresa el número del documento.");
        if (command.Amount <= 0) throw new ArgumentException("El importe debe ser mayor que cero.");
        var pdfBytes = await ReadRequiredAsync(command.Pdf, "PDF del documento", [".pdf"], cancellationToken);

        var number = command.Number.Trim().ToUpperInvariant();
        var isSettlement = command.DocumentType == SpecialDocumentType.CollectionSettlement;
        await EnsureValidInSapAsync(company.Code, ruc, command.IssuedAt, number, command.Amount, isSettlement, cancellationToken);

        var now = clock.GetUtcNow().UtcDateTime;
        var typeName = SpecialTypeNames[command.DocumentType];
        var document = new SupplierDocument
        {
            Number = number,
            EntryType = DocumentEntryType.Special,
            DocumentType = typeName,
            ProviderRuc = ruc,
            ProviderName = $"Proveedor RUC {ruc}",
            Currency = command.Currency,
            Subtotal = command.Amount,
            Amount = command.Amount,
            Concept = $"{typeName} {number}",
            IssuedAt = command.IssuedAt,
            RegisteredAtUtc = now,
            RegisteredById = actor.Id,
            RegisteredByName = actor.RegistrationLabel,
            CompanyId = company.Id,
            Company = company,
            // Pendiente de definir con negocio: flujo.md indica aprobación; la Propuesta 1 registra directo a contabilización.
            Status = DocumentStatus.PendingAccounting,
            Validation = isSettlement ? "Válido en SUNAT y sin duplicidad en SAP (Servicio 02)" : "Sin duplicidad en SAP (Servicio 02)",
        };

        var stored = new List<string>();
        try
        {
            await AttachAsync(document, AttachmentKind.Pdf, command.Pdf!, pdfBytes, stored, cancellationToken);
            AddRegistrationHistory(document, isSettlement ? "Validado en SUNAT y SAP" : "Duplicidad validada en SAP", now);
            documents.Add(document);
            await documents.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await DiscardAsync(stored);
            throw;
        }
        return ToDetail(document);
    }

    // ——— Consultas ———

    public async Task<DocumentPageResponse> SearchAsync(Guid userId, DocumentInbox inbox, string? providerRuc, DocumentStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var actor = await LoadActorAsync(userId, cancellationToken);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = new DocumentQuery(inbox, string.IsNullOrWhiteSpace(providerRuc) ? null : providerRuc.Trim(), status, page, pageSize);

        query = inbox switch
        {
            DocumentInbox.Approvals when actor.IsAdmin => query,
            DocumentInbox.Approvals when actor.IsApprover => query with { ApproverId = actor.Id, ApproverAreaId = actor.AreaId },
            DocumentInbox.Accounting when actor.IsAdmin || actor.IsAccounting => query,
            DocumentInbox.Mine when actor.IsProvider && !actor.IsAdmin => query with { OwnerRuc = actor.Ruc },
            DocumentInbox.Mine when !actor.IsAdmin => query with { RegisteredById = actor.Id },
            DocumentInbox.Mine => query,
            _ => throw new UnauthorizedAccessException("No tienes acceso a esta bandeja."),
        };

        var result = await documents.SearchAsync(query, cancellationToken);
        return new DocumentPageResponse(result.Items.Select(ToSummary).ToArray(), result.Total, page, pageSize, result.CountsByStatus);
    }

    public async Task<DocumentDetailResponse> GetAsync(Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        var (_, document) = await LoadVisibleAsync(userId, documentId, cancellationToken);
        return ToDetail(document);
    }

    public async Task<AttachmentContent> OpenAttachmentAsync(Guid userId, Guid documentId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var (_, document) = await LoadVisibleAsync(userId, documentId, cancellationToken);
        var attachment = document.Attachments.SingleOrDefault(item => item.Id == attachmentId)
            ?? throw new KeyNotFoundException("El archivo no existe.");
        var content = await storage.OpenReadAsync(attachment.StorageKey, cancellationToken);
        return new AttachmentContent(content, attachment.FileName, attachment.ContentType);
    }

    // ——— Acciones ———

    public async Task<DocumentDetailResponse> ApproveAsync(Guid userId, Guid documentId, ApproveDocumentRequest request, CancellationToken cancellationToken)
    {
        var (actor, document) = await LoadForApprovalAsync(userId, documentId, cancellationToken);
        document.Approve(request.ReferenceType, request.Reference, actor.Label, clock.GetUtcNow().UtcDateTime);
        await documents.SaveChangesAsync(cancellationToken);
        return ToDetail(document);
    }

    public async Task<DocumentDetailResponse> RejectAsync(Guid userId, Guid documentId, RejectionStage stage, RejectDocumentRequest request, CancellationToken cancellationToken)
    {
        SupplierDocument document;
        DocumentActor actor;
        if (stage == RejectionStage.Approver)
        {
            (actor, document) = await LoadForApprovalAsync(userId, documentId, cancellationToken);
        }
        else
        {
            actor = await LoadActorAsync(userId, cancellationToken);
            if (!actor.IsAccounting && !actor.IsAdmin) throw new UnauthorizedAccessException("Solo Cuentas por pagar puede rechazar en contabilización.");
            document = await RequireDocumentAsync(documentId, cancellationToken);
        }

        document.Reject(stage, request.Reason, actor.Label, clock.GetUtcNow().UtcDateTime);
        await documents.SaveChangesAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(document.ProviderEmail))
        {
            await NotifyAsync(document.ProviderEmail, $"Documento rechazado: {document.Number}",
                DocumentEmailTemplates.Rejected(document.ProviderName, document.Number, request.Reason.Trim()), cancellationToken);
        }
        return ToDetail(document);
    }

    public async Task<DocumentDetailResponse> ReassignAsync(Guid userId, Guid documentId, ReassignDocumentRequest request, CancellationToken cancellationToken)
    {
        var (actor, document) = await LoadForApprovalAsync(userId, documentId, cancellationToken);
        var approver = await documents.FindApproverAsync(request.ApproverId, cancellationToken)
            ?? throw new ArgumentException("El aprobador seleccionado no es válido.");
        document.Reassign(approver.AreaId, approver.AreaName, approver.UserId, approver.Name, approver.Email, request.Reason, actor.Label, clock.GetUtcNow().UtcDateTime);
        await documents.SaveChangesAsync(cancellationToken);
        await NotifyAsync(approver.Email, $"Documento por aprobar: {document.Number}",
            DocumentEmailTemplates.PendingApproval(approver.Name, document.Number, document.ProviderName, document.Company.Name, FormatAmount(document), request.Reason.Trim()), cancellationToken);
        return ToDetail(document);
    }

    public async Task<DocumentDetailResponse> ObserveAsync(Guid userId, Guid documentId, ObserveDocumentRequest request, CancellationToken cancellationToken)
    {
        var actor = await LoadActorAsync(userId, cancellationToken);
        if (!actor.IsAccounting && !actor.IsAdmin) throw new UnauthorizedAccessException("Solo Cuentas por pagar puede observar documentos.");
        var document = await RequireDocumentAsync(documentId, cancellationToken);
        var email = request.Email.Trim().ToLowerInvariant();
        document.Observe(request.Reason, email, actor.Label, clock.GetUtcNow().UtcDateTime);
        await documents.SaveChangesAsync(cancellationToken);
        await NotifyAsync(email, $"Documento observado: {document.Number}",
            DocumentEmailTemplates.Observed(document.ProviderName, document.Number, request.Reason.Trim()), cancellationToken);
        return ToDetail(document);
    }

    // ——— Utilidades ———

    private async Task<DocumentActor> LoadActorAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await identities.FindByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAccessException("La sesión no es válida.");
        var email = user.Emails.FirstOrDefault(item => item.IsPrimary && item.IsActive)?.Email ?? user.Emails.FirstOrDefault(item => item.IsActive)?.Email ?? string.Empty;
        var roles = user.UserRoles.Where(item => item.Role.IsActive).Select(item => item.Role.Code).ToHashSet();
        return new DocumentActor(user.Id, user.CompanyName, email, user.Ruc, user.AreaId, user.Area?.Name, roles);
    }

    private async Task<(DocumentActor Actor, SupplierDocument Document)> LoadVisibleAsync(Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        var actor = await LoadActorAsync(userId, cancellationToken);
        var document = await RequireDocumentAsync(documentId, cancellationToken);
        var visible = actor.IsAdmin
            || actor.IsAccounting
            || (actor.IsApprover && (document.ApproverId == actor.Id || (actor.AreaId is not null && document.AreaId == actor.AreaId)))
            || (actor.IsProvider && document.ProviderRuc == actor.Ruc)
            || document.RegisteredById == actor.Id;
        if (!visible) throw new KeyNotFoundException("El documento no existe.");
        return (actor, document);
    }

    private async Task<(DocumentActor Actor, SupplierDocument Document)> LoadForApprovalAsync(Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        var actor = await LoadActorAsync(userId, cancellationToken);
        var document = await RequireDocumentAsync(documentId, cancellationToken);
        if (!actor.IsAdmin && !(actor.IsApprover && document.ApproverId == actor.Id))
            throw new UnauthorizedAccessException("Solo el aprobador asignado puede atender este documento.");
        return (actor, document);
    }

    private async Task<SupplierDocument> RequireDocumentAsync(Guid documentId, CancellationToken cancellationToken) =>
        await documents.FindAsync(documentId, cancellationToken) ?? throw new KeyNotFoundException("El documento no existe.");

    private async Task<Company> RequireCompanyAsync(string code, CancellationToken cancellationToken)
    {
        var company = await documents.FindCompanyAsync(code.Trim(), cancellationToken);
        return company is { IsActive: true } ? company : throw new ArgumentException("La sociedad seleccionada no es válida.");
    }

    private async Task EnsureValidInSapAsync(string companyCode, string ruc, DateOnly issuedAt, string number, decimal amount, bool checkSunat, CancellationToken cancellationToken)
    {
        if (await documents.ExistsAsync(ruc, number, cancellationToken))
            throw new DocumentRejectedException($"El documento {number} ya fue registrado para el RUC {ruc}.");
        var validation = await sap.ValidateDocumentAsync(new SapDocumentValidation(companyCode, ruc, issuedAt, number, amount, checkSunat), cancellationToken);
        if (!validation.IsValid)
            throw new DocumentRejectedException(validation.Message ?? "SAP rechazó el documento.");
    }

    private static async Task<byte[]> ReadRequiredAsync(UploadedFile? file, string label, string[] extensions, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0) throw new ArgumentException($"Adjunta el {label}.");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!extensions.Contains(extension))
            throw new ArgumentException($"El {label} debe ser {string.Join(" o ", extensions)}. Recibimos «{Path.GetFileName(file.FileName)}».");
        if (file.Length > MaxFileBytes) throw new ArgumentException($"El {label} supera los 5 MB permitidos.");

        using var buffer = new MemoryStream();
        await file.Content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        if (bytes.Length > MaxFileBytes) throw new ArgumentException($"El {label} supera los 5 MB permitidos.");
        if (!HasExpectedSignature(extension, bytes))
            throw new ArgumentException($"El contenido del {label} no corresponde a un archivo {extension}.");
        return bytes;
    }

    /// <summary>Verifica la firma del archivo para no confiar solo en la extensión.</summary>
    private static bool HasExpectedSignature(string extension, byte[] bytes) => extension switch
    {
        ".pdf" => bytes.AsSpan().StartsWith("%PDF"u8),
        ".zip" => bytes.AsSpan().StartsWith("PK"u8),
        ".xml" => System.Text.Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 512)).TrimStart('﻿', ' ', '\r', '\n', '\t').StartsWith('<'),
        _ => false,
    };

    private byte[] MergeSupport(IReadOnlyList<byte[]> pdfs)
    {
        try { return pdfMerger.Merge(pdfs); }
        catch (InvalidDataException exception) { throw new ArgumentException(exception.Message); }
    }

    private async Task AttachAsync(SupplierDocument document, AttachmentKind kind, UploadedFile file, byte[] bytes, List<string> stored, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var key = await storage.SaveAsync(new MemoryStream(bytes), extension, cancellationToken);
        stored.Add(key);
        document.Attachments.Add(new DocumentAttachment
        {
            DocumentId = document.Id,
            Kind = kind,
            FileName = SafeFileName(file.FileName),
            StorageKey = key,
            ContentType = extension switch { ".pdf" => "application/pdf", ".xml" => "application/xml", ".zip" => "application/zip", _ => "application/octet-stream" },
            SizeBytes = bytes.Length,
        });
    }

    private async Task DiscardAsync(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            try { await storage.DeleteAsync(key, CancellationToken.None); }
            catch (Exception exception) { logger.LogWarning(exception, "No se pudo eliminar el archivo temporal {StorageKey}", key); }
        }
    }

    private static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return clean.Length > 200 ? clean[^200..] : clean;
    }

    private static void AddRegistrationHistory(SupplierDocument document, string validationTitle, DateTime now)
    {
        if (document.OrderNumber is not null)
            document.AddEvent("Orden validada en SAP", $"Servicio 01 SAP · {document.OrderNumber}", DocumentEventKind.Done, now);
        var entry = document.EntryType switch
        {
            DocumentEntryType.WithPurchaseOrder => "Con orden de compra",
            DocumentEntryType.WithoutPurchaseOrder => "Sin orden de compra",
            _ => "Documento especial",
        };
        document.AddEvent("Documento registrado", $"{document.RegisteredByName} · {entry}", DocumentEventKind.Done, now);
        document.AddEvent(validationTitle, "Servicio 02 SAP", DocumentEventKind.Done, now);
        if (document.Status == DocumentStatus.PendingApproval)
        {
            document.AddEvent("Asignado para aprobación", $"{document.ApproverName} · {document.AreaName}", DocumentEventKind.Done, now);
            document.AddEvent("Pendiente de aprobación", $"En revisión de {document.ApproverName}", DocumentEventKind.Current, now);
        }
        else
        {
            document.AddEvent("Pendiente de contabilización", "Cuentas por pagar · Contabilidad", DocumentEventKind.Current, now);
        }
    }

    private async Task NotifyAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        // El correo no debe revertir una acción ya guardada; solo se registra el fallo.
        try { await emailSender.SendAsync(recipient, subject, body, cancellationToken, isHtml: true); }
        catch (Exception exception) { logger.LogWarning(exception, "No se pudo enviar el correo «{Subject}»", subject); }
    }

    private static string FormatAmount(SupplierDocument document) =>
        $"{(document.Currency == Currency.USD ? "US$" : "S/")} {document.Amount.ToString("N2", CultureInfo.GetCultureInfo("es-PE"))}";

    private static CompanyResponse ToResponse(Company company) => new(company.Code, company.Name, company.Ruc);

    private static DocumentSummaryResponse ToSummary(SupplierDocument document) => new(
        document.Id, document.Number, document.EntryType, document.DocumentType, document.ProviderRuc, document.ProviderName,
        document.Currency, document.Amount, document.Status, document.IssuedAt, document.RegisteredAtUtc, document.ApproverName, document.OrderNumber);

    private static DocumentDetailResponse ToDetail(SupplierDocument document) => new(
        document.Id, document.Number, document.EntryType, document.DocumentType, document.ProviderRuc, document.ProviderName, document.ProviderEmail,
        document.Currency, document.Subtotal, document.Igv, document.Amount, document.Concept, document.IssuedAt, document.RegisteredAtUtc,
        document.RegisteredByName, ToResponse(document.Company), document.Status, document.IsPettyCash, document.RejectedBy, document.AreaName, document.ApproverName,
        document.ApproverEmail, document.ApprovedAtUtc, document.ApprovalReferenceType, document.ApprovalReference, document.OrderType,
        document.OrderNumber, document.OrderBalance, document.OrderDescription, document.Validation,
        document.Items.OrderBy(item => item.LineNumber).Select(item => new DocumentItemResponse(item.Description, item.Quantity, item.UnitPrice, item.Amount)).ToArray(),
        document.Attachments.OrderBy(item => item.Kind).ThenBy(item => item.FileName).Select(item => new AttachmentResponse(item.Id, item.Kind, item.FileName, item.SizeBytes)).ToArray(),
        document.Events.OrderBy(item => item.Sequence).Select(item => new DocumentEventResponse(item.Title, item.Actor, item.Kind, item.OccurredAtUtc, item.Note)).ToArray());

    private sealed record DocumentActor(Guid Id, string Name, string Email, string? Ruc, Guid? AreaId, string? AreaName, IReadOnlySet<string> Roles)
    {
        public bool IsAdmin => Roles.Contains(SecurityCatalog.AdministratorRole);
        public bool IsProvider => Roles.Contains(SecurityCatalog.ProviderRole);
        public bool IsInternal => Roles.Contains(SecurityCatalog.InternalUserRole);
        public bool IsApprover => Roles.Contains(SecurityCatalog.AreaApproverRole);
        public bool IsAccounting => Roles.Contains(SecurityCatalog.AccountsPayableRole);
        public bool CanRegister => IsAdmin || IsProvider || IsInternal;
        public string Label => AreaName is null ? Name : $"{Name} · {AreaName}";
        public string RegistrationLabel => $"{Name} ({(IsProvider ? "proveedor" : "interno")})";
    }
}
