using System.Net;

namespace WebProveedores.Application.UseCases.Auth;

internal static class EmailTemplates
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

    public static string InternalAccountActivation(string name, string username, string activationUrl) => Layout(
        $"Hola, {Encode(name)}",
        string.Empty,
        $"El administrador te dio acceso al Portal de Proveedores. Tu usuario es {Encode(username)}. Para activar tu cuenta, crea tu contraseña con el siguiente botón.",
        string.Empty,
        string.Empty,
        "Este enlace vence en 24 horas y solo puede utilizarse una vez.",
        activationUrl,
        showValue: false);

    public static string PasswordReset(string companyName, string resetUrl) => Layout(
        $"Hola, {Encode(companyName)}",
        string.Empty,
        "Recibimos una solicitud para cambiar la contraseña de tu cuenta en el Portal de Proveedores. Para continuar, usa el siguiente botón.",
        string.Empty,
        string.Empty,
        "Este enlace vence en 24 horas y solo puede utilizarse una vez.",
        resetUrl,
        showValue: false,
        actionLabel: "Cambiar mi contraseña");

    public static string EmailVerification(string name, string email, string verifyUrl) => Layout(
        $"Hola, {Encode(name)}",
        string.Empty,
        $"Agregaste {Encode(email)} a tu perfil del Portal de Proveedores. Confirma que es tuyo con el siguiente botón.",
        string.Empty,
        string.Empty,
        "Este enlace vence en 24 horas. Si no fuiste tú, ignora este mensaje.",
        verifyUrl,
        showValue: false,
        actionLabel: "Verificar mi correo");

    private static string Layout(string title, string greeting, string description, string valueLabel, string value, string note, string? actionUrl = null, bool showValue = true, string actionLabel = "Crear mi contraseña") => $"""
        <!doctype html>
        <html lang="es">
        <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><link rel="preconnect" href="https://fonts.googleapis.com"><link rel="preconnect" href="https://fonts.gstatic.com" crossorigin><link href="https://fonts.googleapis.com/css2?family=Outfit:wght@400;500;600;700&display=swap" rel="stylesheet"></head>
        <body style="margin:0;background:#eaf1fb;font-family:'Outfit',Arial,Helvetica,sans-serif;color:#253c6d;">
          <div style="padding:32px 16px;">
            <table role="presentation" align="center" cellpadding="0" cellspacing="0" width="100%" style="max-width:600px;margin:0 auto;">
              <tr><td style="background:#1558c0;padding:28px 36px;border-radius:18px 18px 0 0;color:#fff;">
                <div style="font-size:22px;font-weight:600;letter-spacing:-.3px;"><img src="cid:portal-logo" width="28" height="28" alt="" style="display:inline-block;margin-right:10px;vertical-align:-8px;border:0;">Portal de Proveedores</div>
              </td></tr>
              <tr><td style="background:#fff;padding:38px 40px 34px;border-radius:0 0 18px 18px;">
                <h1 style="margin:0 0 20px;font-size:27px;line-height:1.2;color:#17386f;">{title}</h1>
                {(string.IsNullOrWhiteSpace(greeting) ? "" : $"<p style=\"margin:0 0 14px;font-size:18px;line-height:1.5;color:#253c6d;\">{greeting}</p>")}
                <p style="margin:0 0 26px;font-size:16px;line-height:1.65;color:#405477;">{description}</p>
                {(showValue ? $"<div style=\"margin:0 0 22px;padding:20px 22px;background:#f2f6fc;border:1px solid #d5e0f0;border-radius:12px;\"><div style=\"margin-bottom:8px;font-size:12px;font-weight:600;letter-spacing:.7px;text-transform:uppercase;color:#536784;\">{valueLabel}</div><div style=\"font-size:18px;line-height:1.45;font-weight:600;color:#1558c0;word-break:break-word;\">{Encode(value)}</div></div>" : "")}
                {(actionUrl is null ? "" : $"<a href=\"{Encode(actionUrl)}\" style=\"display:inline-block;margin:0 0 22px;padding:14px 24px;background:#1768e5;color:#fff;text-decoration:none;border-radius:9px;font-size:16px;font-weight:600;\"><img src=\"cid:lock-icon\" width=\"16\" height=\"16\" alt=\"\" style=\"display:inline-block;margin-right:8px;vertical-align:-3px;border:0;\">{actionLabel}</a>")}
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

}
