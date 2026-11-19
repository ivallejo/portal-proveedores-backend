namespace WebProveedores.Domain.Documents;

/// <summary>Orden de compra validada en SAP (Servicio 01). <see cref="Delivery"/> solo en órdenes de servicio.</summary>
public sealed record PurchaseOrderInfo(OrderType Type, string Number, decimal Balance, string Description, DeliveryType? Delivery = null);
