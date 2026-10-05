using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using WebProveedores.Application.Abstractions.Auth;

namespace WebProveedores.Infrastructure.Auth;

/// <summary>
/// Envío de correo desactivado (<c>Smtp:Enabled=false</c>, para desarrollo): no envía nada y deja en el log
/// el destinatario, el asunto y los enlaces del mensaje (activación, cambio de contraseña).
/// </summary>
public sealed partial class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken, bool isHtml = false)
    {
        // Solo los enlaces de acción (no fuentes ni estilos), decodificados para poder copiarlos.
        var links = Links().Matches(body)
            .Select(match => WebUtility.HtmlDecode(match.Groups[1].Value))
            .Where(link => !link.Contains("fonts.g", StringComparison.Ordinal))
            .Distinct();
        logger.LogWarning("Correo NO enviado (Smtp:Enabled=false) a {Recipient}: «{Subject}». Enlaces: {Links}",
            recipient, subject, string.Join(" ", links));
        return Task.CompletedTask;
    }

    [GeneratedRegex("href=\"([^\"]+)\"")]
    private static partial Regex Links();
}
