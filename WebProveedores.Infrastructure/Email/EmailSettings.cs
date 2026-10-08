using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace WebProveedores.Infrastructure.Email;

/// <summary>
/// Modo de correo resuelto y validado al arrancar. Por defecto: <c>Send</c> en producción y <c>Redirect</c>
/// fuera de ella (o <c>Log</c> si no hay buzón de pruebas). Fuera de producción, <c>Send</c> exige
/// <c>Email:AllowSendOutsideProduction=true</c>: un .env mal copiado nunca escribe a proveedores reales.
/// </summary>
public sealed record EmailSettings(EmailMode Mode, string? TestRecipient)
{
    public static EmailSettings Resolve(IConfiguration configuration, bool isProduction)
    {
        // Smtp:TestRecipient y Smtp:RedirectEnabled son los nombres anteriores; se siguen aceptando.
        var testRecipient = Clean(configuration["Email:TestRecipient"]) ?? Clean(configuration["Smtp:TestRecipient"]);
        var mode = ParseMode(configuration["Email:Mode"])
            ?? (configuration.GetValue<bool?>("Smtp:RedirectEnabled") == true ? EmailMode.Redirect : (EmailMode?)null)
            ?? (isProduction ? EmailMode.Send : testRecipient is null ? EmailMode.Log : EmailMode.Redirect);

        if (mode == EmailMode.Redirect && !IsEmail(testRecipient))
            throw new InvalidOperationException("Email:Mode=Redirect necesita un Email:TestRecipient válido.");
        if (mode == EmailMode.Send && !isProduction && !configuration.GetValue("Email:AllowSendOutsideProduction", false))
            throw new InvalidOperationException(
                "Email:Mode=Send fuera de producción enviaría correos a proveedores reales. Usa Redirect o define Email:AllowSendOutsideProduction=true.");
        if (mode != EmailMode.Log)
        {
            var missing = new[] { "Host", "Username", "Password" }.Where(key => Clean(configuration[$"Smtp:{key}"]) is null).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException($"Email:Mode={mode} necesita la configuración SMTP: {string.Join(", ", missing.Select(key => $"Smtp:{key}"))}.");
        }
        return new EmailSettings(mode, mode == EmailMode.Redirect ? testRecipient : null);
    }

    public string Describe() => Mode switch
    {
        EmailMode.Send => "Send (envío real a cada destinatario)",
        EmailMode.Redirect => $"Redirect (todo a {TestRecipient})",
        _ => "Log (no se envía nada; enlaces en el log)",
    };

    private static EmailMode? ParseMode(string? value) =>
        Clean(value) is { } text
            ? Enum.TryParse<EmailMode>(text, ignoreCase: true, out var mode)
                ? mode
                : throw new InvalidOperationException($"Email:Mode «{text}» no es válido. Usa Send, Redirect o Log.")
            : null;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsEmail(string? value) => value is not null && MailAddress.TryCreate(value, out _);
}
