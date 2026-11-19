using WebProveedores.Application.Ports.Outbound.Sap;
using WebProveedores.Application.Ports.Outbound.Sap.Models;
using WebProveedores.Domain.Documents;

namespace WebProveedores.Infrastructure.Sap;

/// <summary>
/// Simulación de los servicios SAP 01 (orden de compra o carrier) y 02 (SUNAT y duplicidad)
/// mientras no se publiquen los endpoints reales. Las órdenes de prueba son las del diseño (Propuesta 4).
/// La duplicidad real la controla la base de datos del portal.
/// </summary>
public sealed class MockSapDocumentGateway : ISapDocumentGateway
{
    private static readonly Dictionary<(OrderType, DeliveryType?, string), (string Description, decimal Balance)> Orders = new()
    {
        [(OrderType.Service, DeliveryType.OnBoard, "4500012873")] = ("Mantenimiento correctivo de compresores – Planta Lurín", 18450m),
        [(OrderType.Service, DeliveryType.OnBoard, "4500012851")] = ("Servicio de calibración de equipos de medición", 7230.50m),
        [(OrderType.Service, DeliveryType.Warehouse, "CR-2026-00527")] = ("Servicio de almacenaje · entrega en almacén Callao", 9860m),
        [(OrderType.Goods, null, "CR-2026-00418")] = ("Repuestos para fajas transportadoras · 3 entregas recibidas", 18450m),
    };

    public Task<SapOrder?> ValidateOrderAsync(string companyCode, OrderType type, DeliveryType? delivery, string number, CancellationToken cancellationToken)
    {
        var found = Orders.TryGetValue((type, delivery, number), out var order)
            ? new SapOrder(number, type, order.Description, order.Balance, delivery)
            : null;
        return Task.FromResult(found);
    }

    public Task<SapValidationResult> ValidateDocumentAsync(SapDocumentValidation request, CancellationToken cancellationToken) =>
        Task.FromResult(SapValidationResult.Valid);
}
