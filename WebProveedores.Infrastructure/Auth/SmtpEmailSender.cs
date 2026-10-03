using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Microsoft.Extensions.Configuration;

namespace WebProveedores.Infrastructure.Auth;

public sealed class SmtpEmailSender(IConfiguration configuration) : IEmailSender
{
    public async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken, bool isHtml = false)
    {
        var host = configuration["Smtp:Host"] ?? throw new InvalidOperationException("SMTP no está configurado.");
        var username = configuration["Smtp:Username"] ?? throw new InvalidOperationException("SMTP username no está configurado.");
        var password = configuration["Smtp:Password"] ?? throw new InvalidOperationException("SMTP password no está configurado.");
        var port = configuration.GetValue("Smtp:Port", 587);
        var from = configuration["Smtp:From"] ?? username;
        var redirectEnabled = configuration.GetValue("Smtp:RedirectEnabled", false);
        var testRecipient = configuration["Smtp:TestRecipient"];
        var targetRecipient = recipient;
        var targetSubject = subject;
        var targetBody = body;

        if (redirectEnabled)
        {
            if (string.IsNullOrWhiteSpace(testRecipient))
                throw new InvalidOperationException("SMTP está en modo prueba, pero Smtp:TestRecipient no está configurado.");

            targetRecipient = testRecipient;
            targetSubject = $"[PRUEBA SMTP] {subject}";
            targetBody = isHtml
                ? $"<div style=\"margin:0 auto 16px;max-width:600px;padding:10px 16px;background:#fff4ed;border:1px solid #ed7624;border-radius:8px;font:14px Arial,sans-serif;color:#7a3a12;\">Correo de prueba · destinatario original: {System.Net.WebUtility.HtmlEncode(recipient)}</div>{body}"
                : $"Destinatario original: {recipient}{Environment.NewLine}{Environment.NewLine}{body}";
        }

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = configuration.GetValue("Smtp:EnableSsl", true),
            Credentials = new NetworkCredential(username, password),
        };
        using var message = new MailMessage(from, targetRecipient, targetSubject, targetBody) { IsBodyHtml = isHtml };
        if (isHtml)
        {
            var htmlView = AlternateView.CreateAlternateViewFromString(targetBody, null, MediaTypeNames.Text.Html);
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
