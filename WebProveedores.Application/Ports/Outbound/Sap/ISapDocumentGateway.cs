using WebProveedores.Application.Ports.Outbound.Sap.Models;
using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Ports.Outbound.Sap;

/// <summary>Servicios SAP del flujo documental (01: orden de compra, 02: SUNAT y duplicidad).</summary>
public interface ISapDocumentGateway
{
    /// <param name="delivery">Tipo de entrega de una orden de servicio (null en órdenes de bien).</param>
    Task<SapOrder?> ValidateOrderAsync(string companyCode, OrderType type, DeliveryType? delivery, string number, CancellationToken cancellationToken);
    Task<SapValidationResult> ValidateDocumentAsync(SapDocumentValidation request, CancellationToken cancellationToken);
}
