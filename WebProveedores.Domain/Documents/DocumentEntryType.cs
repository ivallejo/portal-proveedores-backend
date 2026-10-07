namespace WebProveedores.Domain.Documents;

/// <summary>Cómo ingresó el documento al portal.</summary>
public enum DocumentEntryType
{
    WithPurchaseOrder,
    WithoutPurchaseOrder,
    Special,
}
