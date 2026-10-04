namespace WebProveedores.Domain.Documents;

/// <summary>Cómo ingresó el documento al portal.</summary>
public enum DocumentEntryType
{
    WithPurchaseOrder,
    WithoutPurchaseOrder,
    Special,
}

public enum DocumentStatus
{
    PendingApproval,
    Approved,
    PendingAccounting,
    Accounted,
    Observed,
    Rejected,
}

/// <summary>Quién rechazó el documento.</summary>
public enum RejectionStage
{
    Approver,
    Accounting,
}

public enum OrderType
{
    Goods,
    Service,
}

public enum ApprovalReferenceType
{
    Order,
    Trip,
}

public enum AttachmentKind
{
    Xml,
    Pdf,
    Cdr,
    Support,
}

public enum DocumentEventKind
{
    Done,
    Current,
    Bad,
    Warn,
}

public enum Currency
{
    PEN,
    USD,
}
