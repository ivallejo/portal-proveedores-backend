using WebProveedores.Application.Payments.Queries;
using WebProveedores.Application.Payments.Responses;

namespace WebProveedores.Application.Payments;

/// <summary>Órdenes de pago y estado de facturas del proveedor, consultados en SAP.</summary>
public interface IPaymentQueryService
{
    Task<IReadOnlyList<PaymentOrderResponse>> SearchPaymentOrdersAsync(Guid userId, PaymentSearchQuery request, CancellationToken cancellationToken);
    Task<IReadOnlyList<InvoiceStatusResponse>> SearchInvoicesAsync(Guid userId, InvoiceSearchQuery request, CancellationToken cancellationToken);

    /// <summary>Excel de la misma búsqueda (mismas reglas de acceso y rango).</summary>
    Task<ExportFileResponse> ExportPaymentOrdersAsync(Guid userId, PaymentSearchQuery request, CancellationToken cancellationToken);

    /// <summary>Excel de la misma búsqueda (mismas reglas de acceso y rango).</summary>
    Task<ExportFileResponse> ExportInvoicesAsync(Guid userId, InvoiceSearchQuery request, CancellationToken cancellationToken);
}
