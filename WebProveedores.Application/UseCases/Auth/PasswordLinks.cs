using WebProveedores.Application.Auth;
using WebProveedores.Application.Common.Exceptions;
using WebProveedores.Application.Ports.Outbound.Notifications;
using WebProveedores.Application.Ports.Outbound.Persistence;
using WebProveedores.Domain.Identity;

namespace WebProveedores.Application.UseCases.Auth;

/// <summary>
/// Enlaces de un solo uso (24 h) para activar la cuenta o recuperar la contraseña. Al enviar uno nuevo, los
/// anteriores del mismo tipo que no se usaron quedan reemplazados.
/// </summary>
internal sealed class PasswordLinks(
    IPasswordTokenRepository tokens,
    IUnitOfWork unitOfWork,
    IEmailSender emailSender,
    PortalSettings portal,
    TimeProvider clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    /// <summary>Guarda el token y envía el enlace al correo principal. Devuelve el correo de destino.</summary>
    public async Task<string> SendAsync(AppUser user, PasswordTokenPurpose purpose, CancellationToken cancellationToken)
    {
        var email = user.PrimaryEmail;
        if (string.IsNullOrWhiteSpace(email)) throw new ValidationException("El usuario no tiene un correo principal.");

        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var pending in (await tokens.ListForUserAsync(user.Id, cancellationToken))
                     .Where(item => item.Purpose == purpose && item.UsedAtUtc is null && item.RevokedAtUtc is null))
            pending.RevokedAtUtc = now;

        var token = AuthSupport.NewOneTimeToken();
        tokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = AuthSupport.HashOneTimeToken(token),
            Purpose = purpose,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(Lifetime),
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Los proveedores se identifican con el RUC; el personal interno, con su usuario (DNI).
        var who = user.IsProvider ? $"ruc={Uri.EscapeDataString(user.Ruc!)}" : $"user={Uri.EscapeDataString(user.Username)}";
        if (purpose == PasswordTokenPurpose.Activation)
        {
            var url = portal.Link($"{who}&activationToken={Uri.EscapeDataString(token)}");
            var body = user.IsProvider
                ? EmailTemplates.AccountActivation(user.CompanyName, url)
                : EmailTemplates.InternalAccountActivation(user.CompanyName, user.Username, url);
            await emailSender.SendAsync(email, "Activa tu cuenta - Portal de Proveedores", body, cancellationToken, isHtml: true);
        }
        else
        {
            var url = portal.Link($"{who}&resetToken={Uri.EscapeDataString(token)}");
            await emailSender.SendAsync(email, "Cambia tu contraseña - Portal de Proveedores", EmailTemplates.PasswordReset(user.CompanyName, url), cancellationToken, isHtml: true);
        }
        return email;
    }
}
