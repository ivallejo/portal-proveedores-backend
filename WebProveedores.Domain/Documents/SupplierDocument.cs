namespace WebProveedores.Domain.Documents;

/// <summary>
/// Comprobante registrado en el portal (con o sin orden de compra, o documento especial)
/// con su ciclo de vida: aprobación, contabilización, observación o rechazo.
/// </summary>
public sealed class SupplierDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Number { get; set; } = string.Empty;
    public DocumentEntryType EntryType { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string ProviderRuc { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string? ProviderEmail { get; set; }
    public Currency Currency { get; set; }
    public decimal Subtotal { get; set; }
    public decimal? Igv { get; set; }
    public decimal Amount { get; set; }
    public string Concept { get; set; } = string.Empty;
    public DateOnly IssuedAt { get; set; }
    public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;
    public Guid RegisteredById { get; set; }
    public string RegisteredByName { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public DocumentStatus Status { get; set; }
    public RejectionStage? RejectedBy { get; set; }

    public Guid? AreaId { get; set; }
    public string? AreaName { get; set; }
    public Guid? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public string? ApproverEmail { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public ApprovalReferenceType? ApprovalReferenceType { get; set; }
    public string? ApprovalReference { get; set; }

    public OrderType? OrderType { get; set; }
    public string? OrderNumber { get; set; }
    public decimal? OrderBalance { get; set; }
    public string? OrderDescription { get; set; }

    /// <summary>Resultado de la validación (Servicio 02) mostrado al revisar el documento.</summary>
    public string? Validation { get; set; }

    public Company Company { get; set; } = null!;
    public ICollection<DocumentItem> Items { get; set; } = [];
    public ICollection<DocumentAttachment> Attachments { get; set; } = [];
    public ICollection<DocumentEvent> Events { get; set; } = [];

    public void AddEvent(string title, string actor, DocumentEventKind kind, DateTime occurredAtUtc, string? note = null)
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
            throw new InvalidOperationException("Ingresa el número de pedido o de viaje para aprobar el documento.");

        var label = referenceType == Documents.ApprovalReferenceType.Trip ? "N° de viaje" : "N° de pedido";
        Status = DocumentStatus.PendingAccounting;
        ApprovedAtUtc = now;
        ApprovalReferenceType = referenceType;
        ApprovalReference = reference.Trim().ToUpperInvariant();
        CloseCurrentEvent();
        AddEvent("Documento aprobado", $"Por {actor} · {label} {ApprovalReference}", DocumentEventKind.Done, now);
        AddEvent("Enviado a contabilización", "Pendiente de contabilización · Contabilidad", DocumentEventKind.Current, now);
    }

    public void Reassign(Guid areaId, string areaName, Guid approverId, string approverName, string approverEmail, string actor, DateTime now)
    {
        EnsureStatus(DocumentStatus.PendingApproval, "Solo se pueden reasignar documentos pendientes de aprobación.");
        if (approverId == ApproverId)
            throw new InvalidOperationException("El documento ya está asignado a ese aprobador.");

        AreaId = areaId;
        AreaName = areaName;
        ApproverId = approverId;
        ApproverName = approverName;
        ApproverEmail = approverEmail;
        CloseCurrentEvent();
        AddEvent($"Reasignado a {approverName}", $"Por {actor}", DocumentEventKind.Done, now);
        AddEvent("Pendiente de aprobación", $"En revisión de {approverName} · {areaName}", DocumentEventKind.Current, now);
    }

    public void Reject(RejectionStage stage, string reason, string actor, DateTime now)
    {
        var expected = stage == RejectionStage.Approver ? DocumentStatus.PendingApproval : DocumentStatus.PendingAccounting;
        EnsureStatus(expected, stage == RejectionStage.Approver
            ? "El documento no está pendiente de aprobación."
            : "El documento no está pendiente de contabilización.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Ingresa el motivo del rechazo.");

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
            throw new InvalidOperationException("Ingresa el motivo de la observación.");

        Status = DocumentStatus.Observed;
        CloseCurrentEvent();
        AddEvent("Documento observado", $"Por {actor}", DocumentEventKind.Warn, now, reason.Trim());
        AddEvent("Observación enviada por correo", $"A {email}", DocumentEventKind.Done, now);
    }

    private void EnsureStatus(DocumentStatus expected, string message)
    {
        if (Status != expected) throw new InvalidOperationException(message);
    }

    private void CloseCurrentEvent()
    {
        foreach (var item in Events.Where(item => item.Kind == DocumentEventKind.Current))
            item.Kind = DocumentEventKind.Done;
    }
}

public sealed class DocumentItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public int LineNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
}

public sealed class DocumentAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
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
    public Guid Id { get; set; } = Guid.NewGuid();
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
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Ruc { get; set; }
    public bool IsActive { get; set; } = true;
}
