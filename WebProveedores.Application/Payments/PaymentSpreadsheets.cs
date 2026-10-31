using WebProveedores.Application.Payments.Responses;
using WebProveedores.Application.Ports.Outbound.Files.Models;

namespace WebProveedores.Application.Payments;

/// <summary>Columnas y filas de los Excel de Orden de pago y Estado de factura.</summary>
internal static class PaymentSpreadsheets
{
    private static readonly SpreadsheetColumn[] PaymentOrderColumns =
    [
        new("N° orden de pago"), new("Sociedad"), new("RUC sociedad"), new("Fecha de pago", SpreadsheetFormat.Date),
        new("Forma de pago"), new("Banco"), new("Cuenta"), new("Documento de pago"), new("Moneda"),
        new("Total pagado", SpreadsheetFormat.Amount), new("N° comprobante"), new("Tipo"), new("Fecha de emisión", SpreadsheetFormat.Date),
        new("Importe", SpreadsheetFormat.Amount), new("Retención", SpreadsheetFormat.Amount), new("Detracción", SpreadsheetFormat.Amount),
        new("Pagado", SpreadsheetFormat.Amount), new("Comprobante de retención"), new("Constancia de detracción"),
    ];

    private static readonly SpreadsheetColumn[] InvoiceColumns =
    [
        new("N° de factura"), new("Tipo"), new("RUC"), new("Sociedad"), new("Importe", SpreadsheetFormat.Amount), new("Moneda"),
        new("Fecha de emisión", SpreadsheetFormat.Date), new("Detracción"), new("Retención"), new("Estado"),
    ];

    /// <summary>Una fila por comprobante pagado, con los datos de la orden repetidos (una orden sin comprobantes ocupa una fila).</summary>
    public static Spreadsheet PaymentOrders(IReadOnlyList<PaymentOrderResponse> orders) =>
        new("Órdenes de pago", PaymentOrderColumns, orders
            .SelectMany(order => order.Documents.Count == 0
                ? [OrderRow(order, null)]
                : order.Documents.Select(document => OrderRow(order, document)))
            .ToArray());

    public static Spreadsheet Invoices(IReadOnlyList<InvoiceStatusResponse> invoices) =>
        new("Estado de facturas", InvoiceColumns, invoices
            .Select(invoice => (IReadOnlyList<object?>)
            [
                invoice.Number, invoice.Type, invoice.ProviderRuc, invoice.CompanyName, invoice.Amount, invoice.Currency,
                invoice.IssuedAt, YesNo(invoice.HasDetraction), YesNo(invoice.HasRetention), invoice.Status,
            ])
            .ToArray());

    private static IReadOnlyList<object?> OrderRow(PaymentOrderResponse order, PaidDocumentResponse? document) =>
    [
        order.Number, order.CompanyName, order.CompanyRuc, order.PaidAt, order.PaymentMethod, order.Bank, order.Account,
        order.PaymentDocument, order.Currency, order.Total, document?.Number, document?.Type, document?.IssuedAt, document?.Amount,
        document?.Retention, document?.Detraction, document?.Paid, document?.RetentionDocument, document?.DetractionCertificate,
    ];

    private static string YesNo(bool value) => value ? "Sí" : "No";
}
