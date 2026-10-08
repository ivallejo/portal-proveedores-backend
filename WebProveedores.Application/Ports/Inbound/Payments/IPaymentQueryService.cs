using WebProveedores.Application.Contracts.Payments.Queries;
using WebProveedores.Application.Contracts.Payments.Responses;

namespace WebProveedores.Application.Ports.Inbound.Payments;

/// <summary>Órdenes de pago y estado de facturas del proveedor, consultados en SAP.</summary>
public interface IPaymentQueryService
{
    Task<IReadOnlyList<PaymentOrderResponse>> SearchPaymentOrdersAsync(Guid userId, PaymentSearchQuery request, CancellationToken cancellationToken);
    Task<IReadOnlyList<InvoiceStatusResponse>> SearchInvoicesAsync(Guid userId, InvoiceSearchQuery request, CancellationToken cancellationToken);
}
