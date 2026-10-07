using WebProveedores.Application.Ports.Outbound.Sap.Models;

namespace WebProveedores.Application.Ports.Outbound.Sap;

/// <summary>Consultas de pagos y facturas del proveedor en SAP (zconsopago y zconsfactu).</summary>
public interface ISapPaymentsGateway
{
    Task<IReadOnlyList<SapPaymentOrder>> FindPaymentOrdersAsync(string providerRuc, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<IReadOnlyList<SapInvoice>> FindInvoicesAsync(string providerRuc, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
