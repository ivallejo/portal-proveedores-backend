using System.Globalization;
using Microsoft.Extensions.Logging;
using WebProveedores.Application.Abstractions.Auth;
using WebProveedores.Domain.Documents;

namespace WebProveedores.Application.Documents;

/// <summary>Avisos por correo del flujo documental. Un fallo de correo no revierte la acción ya guardada.</summary>
internal sealed class DocumentNotifier(IEmailSender emailSender, ILogger<DocumentNotifier> logger)
{
    public Task PendingApprovalAsync(string email, string approverName, SupplierDocument document, string companyName, CancellationToken cancellationToken, string? reason = null) =>
        SendAsync(email, $"Documento por aprobar: {document.Number}",
            DocumentEmailTemplates.PendingApproval(approverName, document.Number, document.ProviderName, companyName, FormatAmount(document), reason), cancellationToken);

    public Task RejectedAsync(string email, SupplierDocument document, string reason, CancellationToken cancellationToken) =>
        SendAsync(email, $"Documento rechazado: {document.Number}",
            DocumentEmailTemplates.Rejected(document.ProviderName, document.Number, reason), cancellationToken);

    public Task ObservedAsync(string email, SupplierDocument document, string reason, CancellationToken cancellationToken) =>
        SendAsync(email, $"Documento observado: {document.Number}",
            DocumentEmailTemplates.Observed(document.ProviderName, document.Number, reason), cancellationToken);

    private async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        try { await emailSender.SendAsync(recipient, subject, body, cancellationToken, isHtml: true); }
        catch (Exception exception) { logger.LogWarning(exception, "No se pudo enviar el correo «{Subject}»", subject); }
    }

    private static string FormatAmount(SupplierDocument document) =>
        $"{(document.Currency == Currency.USD ? "US$" : "S/")} {document.Amount.ToString("N2", CultureInfo.GetCultureInfo("es-PE"))}";
}
