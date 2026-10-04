namespace WebProveedores.Application.Abstractions.Auth;

public interface IEmailSender
{
    Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken, bool isHtml = false);
}
