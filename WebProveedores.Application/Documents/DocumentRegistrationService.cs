using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Application.Documents.Commands;
using WebProveedores.Application.Documents.Responses;
using WebProveedores.Application.Ports.Outbound.Persistence;
using WebProveedores.Application.Ports.Outbound.Persistence.Models;
using WebProveedores.Application.Ports.Outbound.Sap;
using WebProveedores.Application.Ports.Outbound.Sap.Models;
using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Documents;

internal sealed class DocumentRegistrationService(
    IDocumentRepository documents,
    IUnitOfWork unitOfWork,
    DocumentAccess access,
    DocumentFiles files,
    ISapDocumentGateway sap,
    DocumentNotifier notifier,
    TimeProvider clock) : IDocumentRegistrationService
{
    private static readonly IReadOnlyDictionary<SpecialDocumentType, string> SpecialTypeNames = new Dictionary<SpecialDocumentType, string>
    {
        [SpecialDocumentType.AirTicket] = "Boleto aéreo",
        [SpecialDocumentType.PublicReceipt] = "Recibo público",
        [SpecialDocumentType.NonDomiciled] = "No domiciliado",
        [SpecialDocumentType.CollectionSettlement] = "Liquidación de cobranzas",
    };

    public async Task<DocumentDetailResponse> RegisterAsync(Guid userId, RegisterElectronicDocumentCommand command, CancellationToken cancellationToken)
    {
        var actor = await access.LoadActorAsync(userId, cancellationToken);
        if (!actor.CanRegister) throw new ForbiddenException("No tienes permiso para registrar documentos.");
        if (command.EntryType == DocumentEntryType.Special)
            throw new ValidationException("Los documentos especiales se registran con su propio formulario.");

        var company = await access.RequireCompanyAsync(actor, command.CompanyCode, cancellationToken);
        var xmlBytes = await DocumentFiles.ReadRequiredAsync(command.Xml, "XML del comprobante", [".xml"], cancellationToken);
        var electronic = UblDocumentReader.Read(new MemoryStream(xmlBytes))
            ?? throw new ValidationException("El XML no es un comprobante electrónico válido (UBL 2.1).");

        if (actor.IsProvider && !actor.IsAdmin && electronic.IssuerRuc != actor.Ruc)
            throw new ValidationException($"El XML fue emitido por el RUC {electronic.IssuerRuc}. Solo puedes registrar documentos emitidos por tu RUC.");
        if (!string.IsNullOrEmpty(company.Ruc) && !string.IsNullOrEmpty(electronic.ReceiverRuc) && electronic.ReceiverRuc != company.Ruc)
            throw new ValidationException($"El receptor del XML (RUC {electronic.ReceiverRuc}) no corresponde a la sociedad {company.Name}.");

        if (command.IsPettyCash)
        {
            if (command.EntryType != DocumentEntryType.WithoutPurchaseOrder)
                throw new ValidationException("Solo los documentos sin orden de compra pueden ser de Caja Chica.");
            if (!actor.IsInternal && !actor.IsAdmin)
                throw new ForbiddenException("Solo el personal interno puede registrar documentos de Caja Chica.");
        }

        var pdfBytes = await DocumentFiles.ReadRequiredAsync(command.Pdf, "PDF del comprobante", [".pdf"], cancellationToken);
        byte[]? cdrBytes = null;
        if (electronic.RequiresCdr)
            cdrBytes = await DocumentFiles.ReadRequiredAsync(command.Cdr, "CDR", [".zip", ".xml"], cancellationToken);
        var extras = new List<byte[]>();
        foreach (var extra in command.Extras)
            extras.Add(await DocumentFiles.ReadRequiredAsync(extra, "archivo de sustento", [".pdf"], cancellationToken));

        SapOrder? order = null;
        if (command.EntryType == DocumentEntryType.WithPurchaseOrder)
        {
            if (command.OrderType is null || string.IsNullOrWhiteSpace(command.OrderNumber))
                throw new ValidationException("Indica el tipo y el número de la orden.");
            order = await sap.ValidateOrderAsync(company.Code, command.OrderType.Value, command.OrderNumber.Trim().ToUpperInvariant(), cancellationToken)
                ?? throw new DocumentRejectedException("SAP no encontró la orden para la sociedad seleccionada o ya no tiene saldo por facturar.");
        }

        // Sin OC pasa por aprobación (proveedor o usuario interno) salvo que sea de Caja Chica.
        var needsApproval = command.EntryType == DocumentEntryType.WithoutPurchaseOrder && !command.IsPettyCash;
        ApproverRecord? approver = null;
        if (needsApproval)
        {
            if (command.ApproverId is null) throw new ValidationException("Selecciona el área y el aprobador del documento.");
            approver = await access.RequireApproverAsync(command.ApproverId.Value, company, cancellationToken);
        }

        await EnsureValidInSapAsync(company.Code, electronic.IssuerRuc, electronic.IssuedAt, electronic.Number, electronic.Total, checkSunat: true, cancellationToken);

        var now = clock.GetUtcNow().UtcDateTime;
        var data = new DocumentData(
            electronic.Number, command.EntryType, electronic.DocumentType, electronic.IssuerRuc,
            string.IsNullOrWhiteSpace(electronic.IssuerName) ? actor.Name : electronic.IssuerName,
            actor.IsProvider ? actor.Email : null,
            electronic.Currency, electronic.Subtotal, electronic.Igv, electronic.Total,
            string.Join("; ", electronic.Lines.Select(line => line.Description)), electronic.IssuedAt,
            "Documento validado en SAP y SUNAT");
        var document = SupplierDocument.Register(
            data, company, actor.Id, actor.RegistrationLabel, now, "Documento validado en SAP y SUNAT", command.IsPettyCash,
            order is null ? null : new PurchaseOrderInfo(order.Type, order.Number, order.Balance, order.Description),
            approver is null ? null : new ApproverAssignment(approver.AreaId, approver.AreaName, approver.UserId, approver.Name, approver.Email));
        foreach (var line in electronic.Lines)
            document.AddItem(line.Description, line.Quantity, line.UnitPrice, line.Amount);

        var stored = new List<string>();
        try
        {
            await files.AttachAsync(document, AttachmentKind.Xml, command.Xml!, xmlBytes, stored, cancellationToken);
            await files.AttachAsync(document, AttachmentKind.Pdf, command.Pdf!, pdfBytes, stored, cancellationToken);
            if (cdrBytes is not null) await files.AttachAsync(document, AttachmentKind.Cdr, command.Cdr!, cdrBytes, stored, cancellationToken);
            if (extras.Count > 0)
            {
                var merged = files.MergePdfs(extras);
                var name = $"Anexos_{electronic.Number}.pdf";
                await files.AttachAsync(document, AttachmentKind.Support, new UploadedFile(name, "application/pdf", merged.Length, Stream.Null), merged, stored, cancellationToken);
            }

            documents.Add(document);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await files.DiscardAsync(stored);
            throw;
        }

        if (approver is not null)
            await notifier.PendingApprovalAsync(approver.Email, approver.Name, document, company.Name, cancellationToken);
        return DocumentMapper.ToDetail(document);
    }

    public async Task<DocumentDetailResponse> RegisterSpecialAsync(Guid userId, RegisterSpecialDocumentCommand command, CancellationToken cancellationToken)
    {
        var actor = await access.LoadActorAsync(userId, cancellationToken);
        if (!actor.IsInternal && !actor.IsAdmin)
            throw new ForbiddenException("Los documentos especiales solo los registra personal interno.");

        var company = await access.RequireCompanyAsync(actor, command.CompanyCode, cancellationToken);
        var ruc = command.ProviderRuc.Trim();
        if (ruc.Length != 11 || !ruc.All(char.IsAsciiDigit)) throw new ValidationException("El RUC del proveedor debe tener 11 dígitos.");
        if (string.IsNullOrWhiteSpace(command.Number)) throw new ValidationException("Ingresa el número del documento.");
        if (command.Amount <= 0) throw new ValidationException("El importe debe ser mayor que cero.");
        var pdfBytes = await DocumentFiles.ReadRequiredAsync(command.Pdf, "PDF del documento", [".pdf"], cancellationToken);

        var number = command.Number.Trim().ToUpperInvariant();
        var isSettlement = command.DocumentType == SpecialDocumentType.CollectionSettlement;
        await EnsureValidInSapAsync(company.Code, ruc, command.IssuedAt, number, command.Amount, isSettlement, cancellationToken);

        var now = clock.GetUtcNow().UtcDateTime;
        var typeName = SpecialTypeNames[command.DocumentType];
        var validation = isSettlement ? "Válido en SUNAT y sin duplicidad en SAP (Servicio 02)" : "Sin duplicidad en SAP (Servicio 02)";
        // Pendiente de definir con negocio: flujo.md indica aprobación; la Propuesta 1 registra directo a contabilización.
        var document = SupplierDocument.Register(
            new DocumentData(number, DocumentEntryType.Special, typeName, ruc, $"Proveedor RUC {ruc}", null,
                command.Currency, command.Amount, null, command.Amount, $"{typeName} {number}", command.IssuedAt, validation),
            company, actor.Id, actor.RegistrationLabel, now, isSettlement ? "Validado en SUNAT y SAP" : "Duplicidad validada en SAP");

        var stored = new List<string>();
        try
        {
            await files.AttachAsync(document, AttachmentKind.Pdf, command.Pdf!, pdfBytes, stored, cancellationToken);
            documents.Add(document);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await files.DiscardAsync(stored);
            throw;
        }
        return DocumentMapper.ToDetail(document);
    }

    private async Task EnsureValidInSapAsync(string companyCode, string ruc, DateOnly issuedAt, string number, decimal amount, bool checkSunat, CancellationToken cancellationToken)
    {
        if (await documents.ExistsAsync(ruc, number, cancellationToken))
            throw new DocumentRejectedException($"El documento {number} ya fue registrado para el RUC {ruc}.");
        var validation = await sap.ValidateDocumentAsync(new SapDocumentValidation(companyCode, ruc, issuedAt, number, amount, checkSunat), cancellationToken);
        if (!validation.IsValid)
            throw new DocumentRejectedException(validation.Message ?? "SAP rechazó el documento.");
    }
}
