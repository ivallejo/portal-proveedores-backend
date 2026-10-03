using System.Net;

namespace WebProveedores.Infrastructure.Auth;

public static class EmailTemplates
{
    public static string AccountActivation(string companyName, string activationUrl) => Layout(
        $"Hola, {Encode(companyName)}",
        string.Empty,
        "Tu empresa fue registrada en el Portal de Proveedores. Para activar tu cuenta, crea tu contraseña con el siguiente botón.",
        string.Empty,
        string.Empty,
        "Este enlace vence en 24 horas y solo puede utilizarse una vez.",
        activationUrl,
        showValue: false);

    public static string PasswordReset(string companyName, string token) => Layout(
        "Recuperación de contraseña",
        $"Hola, {Encode(companyName)}",
        "Recibimos una solicitud para cambiar la contraseña de tu cuenta en el Portal de Proveedores.",
        "Código de recuperación",
        token,
        "Este código vence en 24 horas y solo puede utilizarse una vez.");

    private static string Layout(string title, string greeting, string description, string valueLabel, string value, string note, string? actionUrl = null, bool showValue = true) => $"""
        <!doctype html>
        <html lang="es">
        <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><link rel="preconnect" href="https://fonts.googleapis.com"><link rel="preconnect" href="https://fonts.gstatic.com" crossorigin><link href="https://fonts.googleapis.com/css2?family=Outfit:wght@400;500;600;700&display=swap" rel="stylesheet"></head>
        <body style="margin:0;background:#eaf1fb;font-family:'Outfit',Arial,Helvetica,sans-serif;color:#253c6d;">
          <div style="padding:32px 16px;">
            <table role="presentation" align="center" cellpadding="0" cellspacing="0" width="100%" style="max-width:600px;margin:0 auto;">
              <tr><td style="background:#1558c0;padding:28px 36px;border-radius:18px 18px 0 0;color:#fff;">
                <div style="font-size:22px;font-weight:600;letter-spacing:-.3px;"><span style="display:inline-block;margin-right:10px;vertical-align:-5px;">{PortalIcon()}</span>Portal de Proveedores</div>
              </td></tr>
              <tr><td style="background:#fff;padding:38px 40px 34px;border-radius:0 0 18px 18px;">
                <h1 style="margin:0 0 20px;font-size:27px;line-height:1.2;color:#17386f;">{title}</h1>
                {(string.IsNullOrWhiteSpace(greeting) ? "" : $"<p style=\"margin:0 0 14px;font-size:18px;line-height:1.5;color:#253c6d;\">{greeting}</p>")}
                <p style="margin:0 0 26px;font-size:16px;line-height:1.65;color:#405477;">{description}</p>
                {(showValue ? $"<div style=\"margin:0 0 22px;padding:20px 22px;background:#f2f6fc;border:1px solid #d5e0f0;border-radius:12px;\"><div style=\"margin-bottom:8px;font-size:12px;font-weight:600;letter-spacing:.7px;text-transform:uppercase;color:#536784;\">{valueLabel}</div><div style=\"font-size:18px;line-height:1.45;font-weight:600;color:#1558c0;word-break:break-word;\">{Encode(value)}</div></div>" : "")}
                {(actionUrl is null ? "" : $"<a href=\"{Encode(actionUrl)}\" style=\"display:inline-block;margin:0 0 22px;padding:14px 24px;background:#1768e5;color:#fff;text-decoration:none;border-radius:9px;font-size:16px;font-weight:600;\">{LockIcon()}&nbsp;&nbsp;Crear mi contraseña</a>")}
                <p style="margin:0;font-size:14px;line-height:1.6;color:#536784;">{note}</p>
                <div style="height:1px;margin:26px 0 18px;background:#dbe3ef;"></div>
                <p style="margin:0;font-size:13px;line-height:1.6;color:#6a7890;">Si no solicitaste este correo, puedes ignorarlo. Tu cuenta seguirá segura.</p>
              </td></tr>
              <tr><td style="padding:16px 10px 0;text-align:center;font-size:12px;color:#536784;">© 2026 Portal de Proveedores · Este es un mensaje automático, por favor no respondas.</td></tr>
            </table>
          </div>
        </body>
        </html>
        """;

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private static string PortalIcon() => "<svg width=\"28\" height=\"28\" viewBox=\"0 0 28 28\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\" aria-hidden=\"true\"><path d=\"M14 2.5 24 8v12l-10 5.5L4 20V8l10-5.5Z\" stroke=\"#fff\" stroke-width=\"2.2\"/><path d=\"m14 7 5.5 3v6L14 19l-5.5-3v-6L14 7Z\" fill=\"#fff\"/></svg>";

    private static string LockIcon() => "<svg width=\"16\" height=\"16\" viewBox=\"0 0 16 16\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\" style=\"vertical-align:-2px\" aria-hidden=\"true\"><rect x=\"3\" y=\"7\" width=\"10\" height=\"7\" rx=\"1.5\" stroke=\"#fff\" stroke-width=\"1.4\"/><path d=\"M5 7V5.5a3 3 0 0 1 6 0V7\" stroke=\"#fff\" stroke-width=\"1.4\" stroke-linecap=\"round\"/></svg>";

}
