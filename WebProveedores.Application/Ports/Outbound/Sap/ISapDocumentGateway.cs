using WebProveedores.Application.Ports.Outbound.Sap.Models;
using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Ports.Outbound.Sap;

/// <summary>Servicios SAP del flujo documental (01: orden de compra, 02: SUNAT y duplicidad).</summary>
public interface ISapDocumentGateway
{
    Task<SapOrder?> ValidateOrderAsync(string companyCode, OrderType type, string number, CancellationToken cancellationToken);
    Task<SapValidationResult> ValidateDocumentAsync(SapDocumentValidation request, CancellationToken cancellationToken);
}
