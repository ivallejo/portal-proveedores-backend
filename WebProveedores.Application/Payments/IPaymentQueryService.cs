using WebProveedores.Application.Payments.Queries;
using WebProveedores.Application.Payments.Responses;

namespace WebProveedores.Application.Payments;

/// <summary>Órdenes de pago y estado de facturas del proveedor, consultados en SAP.</summary>
public interface IPaymentQueryService
{
    Task<IReadOnlyList<PaymentOrderResponse>> SearchPaymentOrdersAsync(Guid userId, PaymentSearchQuery request, CancellationToken cancellationToken);
    Task<IReadOnlyList<InvoiceStatusResponse>> SearchInvoicesAsync(Guid userId, InvoiceSearchQuery request, CancellationToken cancellationToken);
}
