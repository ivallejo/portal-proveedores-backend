using WebProveedores.Application.Abstractions.Auth;
using WebProveedores.Application.Auth;
using WebProveedores.Domain.Entities;

namespace WebProveedores.Application.Profile;

/// <summary>Enlace para confirmar que un correo pertenece a la persona (24 h; uno nuevo reemplaza al anterior).</summary>
public sealed class EmailVerifications(IEmailSender emailSender, PortalSettings portal, TimeProvider clock)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    /// <summary>Genera el token (hay que guardar los cambios antes de enviarlo). Devuelve el token para el enlace.</summary>
    public string Start(UserEmail email)
    {
        var token = AuthSupport.NewOneTimeToken();
        email.StartVerification(AuthSupport.HashOneTimeToken(token), clock.GetUtcNow().UtcDateTime.Add(Lifetime));
        return token;
    }

    public Task SendAsync(string name, UserEmail email, string token, CancellationToken cancellationToken) =>
        emailSender.SendAsync(email.Email, "Verifica tu correo - Portal de Proveedores",
            EmailTemplates.EmailVerification(name, email.Email, portal.Link($"emailToken={Uri.EscapeDataString(token)}")),
            cancellationToken, isHtml: true);
}
