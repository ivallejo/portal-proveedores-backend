using WebProveedores.Application.Payments;

namespace WebProveedores.Application.Ports.Inbound.Payments;

/// <summary>Órdenes de pago y estado de facturas del proveedor, consultados en SAP.</summary>
public interface IPaymentQueryService
{
    Task<IReadOnlyList<PaymentOrderResponse>> SearchPaymentOrdersAsync(Guid userId, PaymentSearchRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<InvoiceStatusResponse>> SearchInvoicesAsync(Guid userId, InvoiceSearchRequest request, CancellationToken cancellationToken);
}
