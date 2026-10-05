using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Microsoft.Extensions.Configuration;
using WebProveedores.Application.Abstractions.Auth;

namespace WebProveedores.Infrastructure.Email;

/// <summary>Envío por SMTP (hoy Gmail). La redirección de pruebas la hace <see cref="RedirectingEmailSender"/>.</summary>
public sealed class SmtpEmailSender(IConfiguration configuration) : IEmailSender
{
    public async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken, bool isHtml = false)
    {
        var host = configuration["Smtp:Host"] ?? throw new InvalidOperationException("SMTP no está configurado.");
        var username = configuration["Smtp:Username"] ?? throw new InvalidOperationException("SMTP username no está configurado.");
        var password = configuration["Smtp:Password"] ?? throw new InvalidOperationException("SMTP password no está configurado.");
        var port = configuration.GetValue("Smtp:Port", 587);
        var from = configuration["Smtp:From"] ?? username;
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = configuration.GetValue("Smtp:EnableSsl", true),
            Credentials = new NetworkCredential(username, password),
        };
        using var message = new MailMessage(from, recipient, subject, body) { IsBodyHtml = isHtml };
        if (isHtml)
        {
            var htmlView = AlternateView.CreateAlternateViewFromString(body, null, MediaTypeNames.Text.Html);
            AddInlineResource(htmlView, "portal-logo", "portal-logo.png", MediaTypeNames.Image.Png);
            AddInlineResource(htmlView, "lock-icon", "lock.png", MediaTypeNames.Image.Png);
            message.AlternateViews.Add(htmlView);
        }
        await client.SendMailAsync(message, cancellationToken);
    }

    private static void AddInlineResource(AlternateView view, string contentId, string fileName, string mediaType)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Auth", "EmailAssets", fileName);
        if (!File.Exists(path)) return;
        var resource = new LinkedResource(path, mediaType) { ContentId = contentId, TransferEncoding = TransferEncoding.Base64 };
        view.LinkedResources.Add(resource);
    }
}
