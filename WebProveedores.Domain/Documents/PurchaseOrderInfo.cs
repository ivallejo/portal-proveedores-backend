namespace WebProveedores.Domain.Documents;

/// <summary>Orden de compra validada en SAP (Servicio 01).</summary>
public sealed record PurchaseOrderInfo(OrderType Type, string Number, decimal Balance, string Description);
