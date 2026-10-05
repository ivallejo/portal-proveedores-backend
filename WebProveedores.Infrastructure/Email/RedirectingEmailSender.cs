using System.Net;
using WebProveedores.Application.Abstractions.Auth;

namespace WebProveedores.Infrastructure.Email;

/// <summary>
/// Modo <see cref="EmailMode.Redirect"/>: entrega cada correo solo al buzón de pruebas, con el asunto marcado
/// y un aviso del destinatario original. Nunca escribe al destinatario real.
/// </summary>
public sealed class RedirectingEmailSender(IEmailSender inner, string testRecipient) : IEmailSender
{
    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken, bool isHtml = false)
    {
        var notice = isHtml
            ? $"<div style=\"margin:0 auto 16px;max-width:600px;padding:10px 16px;background:#fff4ed;border:1px solid #ed7624;border-radius:8px;font:14px Arial,sans-serif;color:#7a3a12;\">Correo de prueba · destinatario original: {WebUtility.HtmlEncode(recipient)}</div>{body}"
            : $"Destinatario original: {recipient}{Environment.NewLine}{Environment.NewLine}{body}";
        return inner.SendAsync(testRecipient, $"[PRUEBA] {subject}", notice, cancellationToken, isHtml);
    }
}
