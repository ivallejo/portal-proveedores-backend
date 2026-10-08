using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Application.Documents;
using WebProveedores.Application.Payments.Queries;
using WebProveedores.Application.Payments.Responses;
using WebProveedores.Application.Ports.Outbound.Persistence;
using WebProveedores.Application.Ports.Outbound.Sap;

namespace WebProveedores.Application.Payments;

/// <summary>
/// Reglas de acceso sobre las consultas de SAP: el proveedor solo ve su RUC; Cuentas por pagar y el administrador
/// consultan el RUC que indiquen. Fuera del administrador, solo se muestran las sociedades asignadas al usuario.
/// </summary>
internal sealed class PaymentQueryService(ISapPaymentsGateway sap, DocumentAccess access, ICompanyReader companyReader) : IPaymentQueryService
{
    /// <summary>Rango máximo por consulta, para no sobrecargar SAP.</summary>
    public const int MaxRangeDays = 3 * 366;

    private static readonly IReadOnlyDictionary<string, string> DocumentTypes = new Dictionary<string, string>
    {
        ["01"] = "Factura",
        ["03"] = "Boleta",
        ["07"] = "Nota de crédito",
        ["08"] = "Nota de débito",
        ["12"] = "Ticket",
        ["14"] = "Recibo de servicios públicos",
    };

    public async Task<IReadOnlyList<PaymentOrderResponse>> SearchPaymentOrdersAsync(Guid userId, PaymentSearchQuery request, CancellationToken cancellationToken)
    {
        var (ruc, scope) = await ResolveAsync(userId, request.Ruc, request.From, request.To, cancellationToken);
        var orders = await sap.FindPaymentOrdersAsync(ruc, request.From, request.To, cancellationToken);
        var wanted = request.CompanyCode?.Trim();

        return orders
            .Where(order => scope.Allows(order.CompanyCode) && (string.IsNullOrEmpty(wanted) || order.CompanyCode == wanted))
            .OrderByDescending(order => order.PaidAt)
            .Select(order =>
            {
                var company = scope.ByCode(order.CompanyCode);
                return new PaymentOrderResponse(
                    // El nombre que informa SAP manda: el catálogo del portal puede no tener aún los datos reales.
                    order.Number, order.PaidAt, order.CompanyCode, order.CompanyName ?? company?.Name ?? order.CompanyCode,
                    order.CompanyRuc ?? company?.Ruc, order.ProviderRuc, order.ProviderName, order.Currency, order.Total,
                    PaymentMethod(order.PaymentMethodCode), order.Bank, order.Account, order.PaymentDocument,
                    order.Documents.Select(document => new PaidDocumentResponse(
                        document.Document.Number, DocumentType(document.Document.TypeCode), document.IssuedAt, document.Amount,
                        document.Retention, document.Detraction, document.Paid, document.RetentionDocument, document.DetractionCertificate, document.DetractionRate)).ToArray());
            })
            .ToArray();
    }

    public async Task<IReadOnlyList<InvoiceStatusResponse>> SearchInvoicesAsync(Guid userId, InvoiceSearchQuery request, CancellationToken cancellationToken)
    {
        var (ruc, scope) = await ResolveAsync(userId, request.Ruc, request.From, request.To, cancellationToken);
        // SAP identifica la sociedad de la factura por su RUC: para filtrar, la sociedad debe tenerlo configurado.
        string? wantedRuc = null;
        if (!string.IsNullOrWhiteSpace(request.CompanyCode))
        {
            var company = scope.ByCode(request.CompanyCode.Trim()) ?? throw new ValidationException("La sociedad seleccionada no es válida.");
            wantedRuc = company.Ruc ?? throw new ValidationException($"La sociedad {company.Name} aún no tiene RUC configurado: no se puede filtrar el estado de facturas por ella.");
        }
        var number = request.Number?.Trim().ToUpperInvariant();

        var invoices = await sap.FindInvoicesAsync(ruc, request.From, request.To, cancellationToken);
        return invoices
            .Select(invoice => (Invoice: invoice, Company: scope.ByRuc(invoice.CompanyRuc)))
            // Si la sociedad no se puede identificar (RUC sin configurar), la factura se muestra igual.
            .Where(item => item.Company is null || scope.Allows(item.Company.Code))
            .Where(item => wantedRuc is null || item.Invoice.CompanyRuc == wantedRuc)
            .Where(item => string.IsNullOrEmpty(number) || item.Invoice.Document.Number.Contains(number, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Invoice.IssuedAt)
            .Select(item => new InvoiceStatusResponse(
                item.Invoice.Document.Number, DocumentType(item.Invoice.Document.TypeCode), item.Invoice.ProviderRuc,
                item.Company?.Code, item.Company?.Name, item.Invoice.CompanyRuc, item.Invoice.Amount, item.Invoice.Currency,
                item.Invoice.IssuedAt, item.Invoice.HasDetraction, item.Invoice.HasRetention, item.Invoice.Status))
            .ToArray();
    }

    private async Task<(string Ruc, CompanyScope Scope)> ResolveAsync(Guid userId, string? requestedRuc, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (from > to) throw new ValidationException("La fecha inicial no puede ser posterior a la final.");
        if (to.DayNumber - from.DayNumber > MaxRangeDays) throw new ValidationException("El rango de fechas no puede superar los 3 años.");

        var actor = await access.LoadActorAsync(userId, cancellationToken);
        string ruc;
        if (actor.IsProvider && !actor.IsAdmin)
        {
            ruc = actor.Ruc ?? throw new ForbiddenException("Tu usuario no tiene un RUC asociado.");
        }
        else if (actor.IsAdmin || actor.IsAccounting)
        {
            ruc = requestedRuc?.Trim() ?? string.Empty;
            if (ruc.Length != 11 || !ruc.All(char.IsAsciiDigit)) throw new ValidationException("Ingresa el RUC del proveedor (11 dígitos).");
        }
        else
        {
            throw new ForbiddenException("No tienes acceso a los pagos de proveedores.");
        }

        var companies = await companyReader.ListActiveAsync(cancellationToken);
        return (ruc, new CompanyScope(actor, companies));
    }

    private static string DocumentType(string code) => DocumentTypes.TryGetValue(code, out var name) ? name : "Comprobante";

    private static string PaymentMethod(string code) => code.Trim().ToUpperInvariant() switch
    {
        "T" => "Transferencia bancaria",
        "C" => "Cheque",
        var other => other,
    };
}
