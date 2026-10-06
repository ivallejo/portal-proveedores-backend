namespace WebProveedores.Domain.Documents;

/// <summary>Datos del comprobante tal como llegan del XML o del formulario de documento especial.</summary>
public sealed record DocumentData(
    string Number,
    DocumentEntryType EntryType,
    string DocumentType,
    string ProviderRuc,
    string ProviderName,
    string? ProviderEmail,
    Currency Currency,
    decimal Subtotal,
    decimal? Igv,
    decimal Amount,
    string Concept,
    DateOnly IssuedAt,
    string Validation);

/// <summary>Orden de compra validada en SAP (Servicio 01).</summary>
public sealed record PurchaseOrderInfo(OrderType Type, string Number, decimal Balance, string Description);

/// <summary>Aprobador de área asignado al documento.</summary>
public sealed record ApproverAssignment(Guid AreaId, string AreaName, Guid ApproverId, string Name, string Email);

/// <summary>
/// Comprobante registrado en el portal (con o sin orden de compra, o documento especial)
/// con su ciclo de vida: aprobación, contabilización, observación o rechazo.
/// </summary>
public sealed class SupplierDocument
{
    // Para EF Core.
    private SupplierDocument() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string Number { get; private set; } = string.Empty;
    public DocumentEntryType EntryType { get; private set; }
    public string DocumentType { get; private set; } = string.Empty;
    public string ProviderRuc { get; private set; } = string.Empty;
    public string ProviderName { get; private set; } = string.Empty;
    public string? ProviderEmail { get; private set; }
    public Currency Currency { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal? Igv { get; private set; }
    public decimal Amount { get; private set; }
    public string Concept { get; private set; } = string.Empty;
    public DateOnly IssuedAt { get; private set; }
    public DateTime RegisteredAtUtc { get; private set; }
    public Guid RegisteredById { get; private set; }
    public string RegisteredByName { get; private set; } = string.Empty;
    public Guid CompanyId { get; private set; }
    public DocumentStatus Status { get; private set; }
    public RejectionStage? RejectedBy { get; private set; }

    /// <summary>Documento de Caja Chica: se registra sin pasar por aprobación.</summary>
    public bool IsPettyCash { get; private set; }

    public Guid? AreaId { get; private set; }
    public string? AreaName { get; private set; }
    public Guid? ApproverId { get; private set; }
    public string? ApproverName { get; private set; }
    public string? ApproverEmail { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public ApprovalReferenceType? ApprovalReferenceType { get; private set; }
    public string? ApprovalReference { get; private set; }

    public OrderType? OrderType { get; private set; }
    public string? OrderNumber { get; private set; }
    public decimal? OrderBalance { get; private set; }
    public string? OrderDescription { get; private set; }

    /// <summary>Resultado de la validación (Servicio 02) mostrado al revisar el documento.</summary>
    public string? Validation { get; private set; }

    public Company Company { get; private set; } = null!;
    public ICollection<DocumentItem> Items { get; private set; } = [];
    public ICollection<DocumentAttachment> Attachments { get; private set; } = [];
    public ICollection<DocumentEvent> Events { get; private set; } = [];

    /// <summary>
    /// Registra un documento aplicando las reglas del flujo: Con OC exige la orden; Sin OC pasa por aprobación
    /// salvo Caja Chica; los especiales van directo a contabilización. Deja el historial inicial.
    /// </summary>
    /// <param name="validationTitle">Título del evento de validación en SAP/SUNAT para el historial.</param>
    public static SupplierDocument Register(DocumentData data, Company company, Guid registeredById, string registeredByName,
        DateTime now, string validationTitle, bool isPettyCash = false, PurchaseOrderInfo? order = null, ApproverAssignment? approver = null)
    {
        switch (data.EntryType)
        {
            case DocumentEntryType.WithPurchaseOrder when order is null:
                throw new DomainRuleException("Un documento con orden de compra necesita la orden validada.");
            case DocumentEntryType.WithoutPurchaseOrder when !isPettyCash && approver is null:
                throw new DomainRuleException("Un documento sin orden de compra necesita un aprobador.");
        }
        if (isPettyCash && data.EntryType != DocumentEntryType.WithoutPurchaseOrder)
            throw new DomainRuleException("Solo los documentos sin orden de compra pueden ser de Caja Chica.");
        if (approver is not null && (isPettyCash || data.EntryType != DocumentEntryType.WithoutPurchaseOrder))
            throw new DomainRuleException("Solo los documentos sin orden de compra (no Caja Chica) pasan por aprobación.");
        if (order is not null && data.EntryType != DocumentEntryType.WithPurchaseOrder)
            throw new DomainRuleException("Solo los documentos con orden de compra llevan orden.");

        var document = new SupplierDocument
        {
            Number = data.Number,
            EntryType = data.EntryType,
            DocumentType = data.DocumentType,
            ProviderRuc = data.ProviderRuc,
            ProviderName = data.ProviderName,
            ProviderEmail = data.ProviderEmail,
            Currency = data.Currency,
            Subtotal = data.Subtotal,
            Igv = data.Igv,
            Amount = data.Amount,
            Concept = data.Concept,
            IssuedAt = data.IssuedAt,
            Validation = data.Validation,
            RegisteredAtUtc = now,
            RegisteredById = registeredById,
            RegisteredByName = registeredByName,
            CompanyId = company.Id,
            Company = company,
            IsPettyCash = isPettyCash,
            Status = approver is null ? DocumentStatus.PendingAccounting : DocumentStatus.PendingApproval,
            OrderType = order?.Type,
            OrderNumber = order?.Number,
            OrderBalance = order?.Balance,
            OrderDescription = order?.Description,
            AreaId = approver?.AreaId,
            AreaName = approver?.AreaName,
            ApproverId = approver?.ApproverId,
            ApproverName = approver?.Name,
            ApproverEmail = approver?.Email,
        };
        document.AddRegistrationHistory(validationTitle, now);
        return document;
    }

    public void AddItem(string description, decimal quantity, decimal unitPrice, decimal amount) =>
        Items.Add(new DocumentItem { DocumentId = Id, LineNumber = Items.Count + 1, Description = description, Quantity = quantity, UnitPrice = unitPrice, Amount = amount });

    public DocumentAttachment AddAttachment(AttachmentKind kind, string fileName, string storageKey, string contentType, long sizeBytes, DateTime now)
    {
        var attachment = new DocumentAttachment { DocumentId = Id, Kind = kind, FileName = fileName, StorageKey = storageKey, ContentType = contentType, SizeBytes = sizeBytes, UploadedAtUtc = now };
        Attachments.Add(attachment);
        return attachment;
    }

    private void AddRegistrationHistory(string validationTitle, DateTime now)
    {
        if (OrderNumber is not null)
            AddEvent("Orden validada en SAP", $"Servicio 01 SAP · {OrderNumber}", DocumentEventKind.Done, now);
        var entry = EntryType switch
        {
            DocumentEntryType.WithPurchaseOrder => "Con orden de compra",
            DocumentEntryType.WithoutPurchaseOrder => "Sin orden de compra",
            _ => "Documento especial",
        };
        AddEvent("Documento registrado", $"{RegisteredByName} · {entry}", DocumentEventKind.Done, now);
        AddEvent(validationTitle, "Servicio 02 SAP", DocumentEventKind.Done, now);
        if (Status == DocumentStatus.PendingApproval)
        {
            AddEvent("Asignado para aprobación", $"{ApproverName} · {AreaName}", DocumentEventKind.Done, now);
            AddEvent("Pendiente de aprobación", $"En revisión de {ApproverName}", DocumentEventKind.Current, now);
        }
        else
        {
            AddEvent("Pendiente de contabilización", "Cuentas por pagar · Contabilidad", DocumentEventKind.Current, now);
        }
    }

    private void AddEvent(string title, string actor, DocumentEventKind kind, DateTime occurredAtUtc, string? note = null)
    {
        Events.Add(new DocumentEvent
        {
            DocumentId = Id,
            Sequence = Events.Count == 0 ? 1 : Events.Max(item => item.Sequence) + 1,
            Title = title,
            Actor = actor,
            Kind = kind,
            OccurredAtUtc = occurredAtUtc,
            Note = note,
        });
    }

    public void Approve(ApprovalReferenceType referenceType, string reference, string actor, DateTime now)
    {
        EnsureStatus(DocumentStatus.PendingApproval, "El documento no está pendiente de aprobación.");
        if (string.IsNullOrWhiteSpace(reference))
            throw new DomainRuleException("Ingresa el número de pedido o de viaje para aprobar el documento.");

        var label = referenceType == Documents.ApprovalReferenceType.Trip ? "N° de viaje" : "N° de pedido";
        Status = DocumentStatus.PendingAccounting;
        ApprovedAtUtc = now;
        ApprovalReferenceType = referenceType;
        ApprovalReference = reference.Trim().ToUpperInvariant();
        CloseCurrentEvent();
        AddEvent("Documento aprobado", $"Por {actor} · {label} {ApprovalReference}", DocumentEventKind.Done, now);
        AddEvent("Enviado a contabilización", "Pendiente de contabilización · Contabilidad", DocumentEventKind.Current, now);
    }

    public void Reassign(ApproverAssignment approver, string reason, string actor, DateTime now)
    {
        EnsureStatus(DocumentStatus.PendingApproval, "Solo se pueden reasignar documentos pendientes de aprobación.");
        if (approver.ApproverId == ApproverId)
            throw new DomainRuleException("El documento ya está asignado a ese aprobador.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainRuleException("Ingresa el motivo de la reasignación.");

        AreaId = approver.AreaId;
        AreaName = approver.AreaName;
        ApproverId = approver.ApproverId;
        ApproverName = approver.Name;
        ApproverEmail = approver.Email;
        CloseCurrentEvent();
        AddEvent($"Reasignado a {approver.Name}", $"Por {actor}", DocumentEventKind.Done, now, reason.Trim());
        AddEvent("Pendiente de aprobación", $"En revisión de {approver.Name} · {approver.AreaName}", DocumentEventKind.Current, now);
    }

    public void Reject(RejectionStage stage, string reason, string actor, DateTime now)
    {
        var expected = stage == RejectionStage.Approver ? DocumentStatus.PendingApproval : DocumentStatus.PendingAccounting;
        EnsureStatus(expected, stage == RejectionStage.Approver
            ? "El documento no está pendiente de aprobación."
            : "El documento no está pendiente de contabilización.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainRuleException("Ingresa el motivo del rechazo.");

        Status = DocumentStatus.Rejected;
        RejectedBy = stage;
        CloseCurrentEvent();
        AddEvent("Documento rechazado", $"Por {actor}", DocumentEventKind.Bad, now, reason.Trim());
        AddEvent("Proveedor notificado por correo", string.IsNullOrWhiteSpace(ProviderEmail) ? "Sistema" : $"A {ProviderEmail}", DocumentEventKind.Done, now);
    }

    public void Observe(string reason, string email, string actor, DateTime now)
    {
        EnsureStatus(DocumentStatus.PendingAccounting, "El documento no está pendiente de contabilización.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainRuleException("Ingresa el motivo de la observación.");

        Status = DocumentStatus.Observed;
        CloseCurrentEvent();
        AddEvent("Documento observado", $"Por {actor}", DocumentEventKind.Warn, now, reason.Trim());
        AddEvent("Observación enviada por correo", $"A {email}", DocumentEventKind.Done, now);
    }

    private void EnsureStatus(DocumentStatus expected, string message)
    {
        if (Status != expected) throw new DomainRuleException(message);
    }

    private void CloseCurrentEvent()
    {
        foreach (var item in Events.Where(item => item.Kind == DocumentEventKind.Current))
            item.Kind = DocumentEventKind.Done;
    }
}

public sealed class DocumentItem
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid DocumentId { get; set; }
    public int LineNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
}

public sealed class DocumentAttachment
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid DocumentId { get; set; }
    public AttachmentKind Kind { get; set; }
    public string FileName { get; set; } = string.Empty;
    /// <summary>Clave interna en el almacenamiento; nunca se expone al cliente.</summary>
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class DocumentEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid DocumentId { get; set; }
    public int Sequence { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DocumentEventKind Kind { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? Note { get; set; }
}

/// <summary>Sociedad del grupo que recibe el documento.</summary>
public sealed class Company
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Ruc { get; set; }
    /// <summary>Correo donde la sociedad recibe los comprobantes electrónicos de sus proveedores.</summary>
    public string? BillingEmail { get; set; }
    public bool IsActive { get; set; } = true;
}
