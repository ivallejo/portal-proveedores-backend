namespace WebProveedores.Domain.Documents;

public enum DocumentInbox
{
    /// <summary>Documentos que pasaron o están en aprobación.</summary>
    Approvals,
    /// <summary>Documentos que llegaron a Cuentas por pagar.</summary>
    Accounting,
    /// <summary>Documentos registrados por el usuario o emitidos con su RUC.</summary>
    Mine,
}
