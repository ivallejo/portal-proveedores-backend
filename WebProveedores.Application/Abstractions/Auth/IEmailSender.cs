namespace WebProveedores.Application.Abstractions.Auth;

public interface IEmailSender
{
    /// <param name="copyTo">Destinatarios en copia (por ejemplo, el correo de facturación de la sociedad).</param>
    Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken, bool isHtml = false, IReadOnlyList<string>? copyTo = null);
}
