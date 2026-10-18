using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using WebProveedores.Application.Ports.Outbound.Notifications;
using WebProveedores.Application.Ports.Outbound.Notifications.Models;

namespace WebProveedores.Infrastructure.Email;

/// <summary>
/// Modo <see cref="EmailMode.Log"/>: no envía nada y deja en el log
/// el destinatario, el asunto y los enlaces del mensaje (activación, cambio de contraseña).
/// </summary>
public sealed partial class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken, bool isHtml = false, IReadOnlyList<string>? copyTo = null, IReadOnlyList<EmailAttachment>? attachments = null)
    {
        // Solo los enlaces de acción (no fuentes ni estilos), decodificados para poder copiarlos.
        var links = Links().Matches(body)
            .Select(match => WebUtility.HtmlDecode(match.Groups[1].Value))
            .Where(link => !link.Contains("fonts.g", StringComparison.Ordinal))
            .Distinct();
        var files = (attachments ?? []).Select(file => $"{file.FileName} ({file.Content.Length / 1024} KB)");
        logger.LogWarning("Correo NO enviado (Email:Mode=Log) a {Recipient} (copia: {CopyTo}): «{Subject}». Enlaces: {Links}. Adjuntos: {Attachments}",
            recipient, string.Join(", ", copyTo ?? []), subject, string.Join(" ", links), string.Join(", ", files));
        return Task.CompletedTask;
    }

    [GeneratedRegex("href=\"([^\"]+)\"")]
    private static partial Regex Links();
}
