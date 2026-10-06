using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebProveedores.Api.Infrastructure;
using WebProveedores.Application.Payments;

namespace WebProveedores.Api.Controllers;

/// <summary>Órdenes de pago y estado de facturas del proveedor (consultas en línea a SAP).</summary>
[ApiController]
[Route("api")]
[Authorize(Policy = Policies.PaymentsView)]
public sealed class PaymentsController(IPaymentQueryService payments, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Pagos realizados entre dos fechas. El proveedor consulta su RUC; CxP y el administrador indican <c>ruc</c>.</summary>
    [HttpGet("payment-orders")]
    public async Task<ActionResult<IReadOnlyList<PaymentOrderResponse>>> PaymentOrders(
        [FromQuery, Required] DateOnly from,
        [FromQuery, Required] DateOnly to,
        [FromQuery] string? ruc,
        [FromQuery] string? company,
        CancellationToken cancellationToken) =>
        Ok(await payments.SearchPaymentOrdersAsync(currentUser.Id, new PaymentSearchRequest(ruc, company, from, to), cancellationToken));

    /// <summary>Facturas emitidas entre dos fechas con su estado en SAP.</summary>
    [HttpGet("invoices")]
    public async Task<ActionResult<IReadOnlyList<InvoiceStatusResponse>>> Invoices(
        [FromQuery, Required] DateOnly from,
        [FromQuery, Required] DateOnly to,
        [FromQuery] string? ruc,
        [FromQuery] string? company,
        [FromQuery] string? number,
        CancellationToken cancellationToken) =>
        Ok(await payments.SearchInvoicesAsync(currentUser.Id, new InvoiceSearchRequest(ruc, company, number, from, to), cancellationToken));
}
