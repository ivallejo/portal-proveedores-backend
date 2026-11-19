namespace WebProveedores.Domain.Documents;

/// <summary>Tipo de entrega de una orden de servicio: a bordo (N° de orden de compra) o en almacén (N° de carrier).</summary>
public enum DeliveryType
{
    OnBoard,
    Warehouse,
}
