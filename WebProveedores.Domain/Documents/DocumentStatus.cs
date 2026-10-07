namespace WebProveedores.Domain.Documents;

public enum DocumentStatus
{
    PendingApproval,
    Approved,
    PendingAccounting,
    Accounted,
    Observed,
    Rejected,
}
