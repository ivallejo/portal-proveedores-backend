using WebProveedores.Application.Abstractions.Documents;
using WebProveedores.Domain.Documents;

namespace WebProveedores.Infrastructure.Documents;

/// <summary>
/// Simulación de los servicios SAP 01 (orden de compra) y 02 (SUNAT y duplicidad)
/// mientras no se publiquen los endpoints reales. Las órdenes de prueba son las de la Propuesta 1.
/// La duplicidad real la controla la base de datos del portal.
/// </summary>
public sealed class MockSapDocumentGateway : ISapDocumentGateway
{
    private static readonly Dictionary<(OrderType, string), (string Description, decimal Balance)> Orders = new()
    {
        [(OrderType.Service, "4500012873")] = ("Mantenimiento correctivo de compresores – Planta Lurín", 18450m),
        [(OrderType.Service, "4500012851")] = ("Servicio de calibración de equipos de medición", 7230.50m),
        [(OrderType.Goods, "CR-2026-00418")] = ("Repuestos para fajas transportadoras · 3 entregas recibidas", 18450m),
    };

    public Task<SapOrder?> ValidateOrderAsync(string companyCode, OrderType type, string number, CancellationToken cancellationToken)
    {
        var found = Orders.TryGetValue((type, number), out var order)
            ? new SapOrder(number, type, order.Description, order.Balance)
            : null;
        return Task.FromResult(found);
    }

    public Task<SapValidationResult> ValidateDocumentAsync(SapDocumentValidation request, CancellationToken cancellationToken) =>
        Task.FromResult(SapValidationResult.Valid);
}
