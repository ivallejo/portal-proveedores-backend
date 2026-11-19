using WebProveedores.Domain.Common;

namespace WebProveedores.Domain.Documents;

/// <summary>Reglas del tipo de entrega según el tipo de orden.</summary>
public static class OrderDelivery
{
    /// <summary>
    /// Las órdenes de bien no llevan tipo de entrega; las de servicio se entregan a bordo salvo que se indique almacén.
    /// </summary>
    public static DeliveryType? Resolve(OrderType type, DeliveryType? delivery) => type switch
    {
        OrderType.Goods when delivery is not null => throw new DomainRuleException("El tipo de entrega solo aplica a órdenes de servicio."),
        OrderType.Goods => null,
        _ => delivery ?? DeliveryType.OnBoard,
    };

    /// <summary>Con bien o con servicio entregado en almacén se valida un N° de carrier; si no, el N° de orden de compra.</summary>
    public static bool IsCarrier(OrderType type, DeliveryType? delivery) =>
        type == OrderType.Goods || delivery == DeliveryType.Warehouse;
}
