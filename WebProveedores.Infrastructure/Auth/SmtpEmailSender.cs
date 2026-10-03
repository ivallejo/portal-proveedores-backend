using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace WebProveedores.Infrastructure.Auth;

public sealed class SmtpEmailSender(IConfiguration configuration) : IEmailSender
{
    public async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
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
            targetBody = $"Destinatario original: {recipient}{Environment.NewLine}{Environment.NewLine}{body}";
        }

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = configuration.GetValue("Smtp:EnableSsl", true),
            Credentials = new NetworkCredential(username, password),
        };
        using var message = new MailMessage(from, targetRecipient, targetSubject, targetBody);
        await client.SendMailAsync(message, cancellationToken);
    }
}
