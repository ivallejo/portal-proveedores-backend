using System.Globalization;
using Microsoft.Extensions.Logging;
using WebProveedores.Application.Ports.Outbound.Notifications;
using WebProveedores.Application.Ports.Outbound.Notifications.Models;
using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Documents;

/// <summary>Avisos por correo del flujo documental. Un fallo de correo no revierte la acción ya guardada.</summary>
internal sealed class DocumentNotifier(IEmailSender emailSender, ILogger<DocumentNotifier> logger)
{
    public Task PendingApprovalAsync(string email, string approverName, SupplierDocument document, string companyName, CancellationToken cancellationToken, string? reason = null) =>
        SendAsync(email, $"Documento por aprobar: {document.Number}",
            DocumentEmailTemplates.PendingApproval(approverName, document.Number, document.ProviderName, companyName, FormatAmount(document), reason), cancellationToken);

    /// <summary>Aviso al proveedor, con copia al correo de facturación de la sociedad.</summary>
    public Task RejectedAsync(string email, SupplierDocument document, string reason, CancellationToken cancellationToken) =>
        SendAsync(email, $"Documento rechazado: {document.Number}",
            DocumentEmailTemplates.Rejected(document.ProviderName, document.Number, reason), cancellationToken, CompanyCopy(document));

    /// <summary>
    /// Aviso al usuario indicado por Cuentas por pagar con el motivo y los archivos del documento (flujo 05),
    /// con copia al correo de facturación de la sociedad. Sin <paramref name="attachments"/> (superan el tope o no se
    /// pudieron leer), el correo pide revisarlos en el portal.
    /// </summary>
    public Task ObservedAsync(string email, SupplierDocument document, string reason, IReadOnlyList<EmailAttachment>? attachments, CancellationToken cancellationToken) =>
        SendAsync(email, $"Documento observado: {document.Number}",
            DocumentEmailTemplates.Observed(document.ProviderName, document.Number, reason, attachments is not null), cancellationToken, CompanyCopy(document), attachments);

    private static IReadOnlyList<string>? CompanyCopy(SupplierDocument document) =>
        string.IsNullOrWhiteSpace(document.Company?.BillingEmail) ? null : [document.Company.BillingEmail];

    private async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken, IReadOnlyList<string>? copyTo = null,
        IReadOnlyList<EmailAttachment>? attachments = null)
    {
        try { await emailSender.SendAsync(recipient, subject, body, cancellationToken, isHtml: true, copyTo, attachments); }
        catch (Exception exception) { logger.LogWarning(exception, "No se pudo enviar el correo «{Subject}»", subject); }
    }

    private static string FormatAmount(SupplierDocument document) =>
        $"{(document.Currency == Currency.USD ? "US$" : "S/")} {document.Amount.ToString("N2", CultureInfo.GetCultureInfo("es-PE"))}";
}
