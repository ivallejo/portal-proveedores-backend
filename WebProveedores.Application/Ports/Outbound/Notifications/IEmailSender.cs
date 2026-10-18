using WebProveedores.Application.Ports.Outbound.Notifications.Models;

namespace WebProveedores.Application.Ports.Outbound.Notifications;

public interface IEmailSender
{
    /// <param name="copyTo">Destinatarios en copia (por ejemplo, el correo de facturación de la sociedad).</param>
    /// <param name="attachments">Archivos adjuntos (por ejemplo, los de un documento observado).</param>
    Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken, bool isHtml = false, IReadOnlyList<string>? copyTo = null, IReadOnlyList<EmailAttachment>? attachments = null);
}
