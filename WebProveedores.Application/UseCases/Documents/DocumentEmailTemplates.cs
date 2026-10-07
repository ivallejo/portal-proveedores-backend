using System.Net;

namespace WebProveedores.Application.UseCases.Documents;

/// <summary>Correos del flujo documental, con el mismo estilo que los correos de acceso.</summary>
internal static class DocumentEmailTemplates
{
    public static string PendingApproval(string approverName, string number, string providerName, string companyName, string amount, string? reassignmentReason = null) => Layout(
        $"Hola, {Encode(approverName)}",
        reassignmentReason is null ? "Tienes un documento por aprobar en el Portal de Proveedores." : "Te reasignaron un documento por aprobar en el Portal de Proveedores.",
        [("Documento", number), ("Proveedor", providerName), ("Sociedad", companyName), ("Importe", amount), .. reassignmentReason is null ? Array.Empty<(string, string)>() : new[] { ("Motivo de reasignación", reassignmentReason) }],
        "Ingresa al portal, en el módulo Documentos, para aprobarlo, rechazarlo o reasignarlo.");

    public static string Rejected(string providerName, string number, string reason) => Layout(
        $"Hola, {Encode(providerName)}",
        $"Tu documento {Encode(number)} fue rechazado.",
        [("Motivo", reason)],
        "Si tienes dudas, comunícate con el área que solicitó el servicio.");

    public static string Observed(string providerName, string number, string reason) => Layout(
        $"Hola, {Encode(providerName)}",
        $"Cuentas por pagar observó tu documento {Encode(number)}.",
        [("Observación", reason)],
        "Levanta la observación respondiendo con la información solicitada.");

    private static string Layout(string title, string description, IEnumerable<(string Label, string Value)> fields, string note)
    {
        var rows = string.Concat(fields.Select(field =>
            $"<tr><td style=\"padding:8px 0;font-size:13px;color:#5a6477;width:120px;vertical-align:top;\">{Encode(field.Label)}</td><td style=\"padding:8px 0;font-size:15px;font-weight:700;color:#0e2a5c;\">{Encode(field.Value)}</td></tr>"));
        return $"""
            <!doctype html>
            <html lang="es">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
            <body style="margin:0;background:#eef4fd;font-family:Arial,Helvetica,sans-serif;color:#0e2a5c;">
              <div style="padding:32px 16px;">
                <table role="presentation" align="center" cellpadding="0" cellspacing="0" width="100%" style="max-width:600px;margin:0 auto;">
                  <tr><td style="background:#1668e3;padding:24px 36px;border-radius:18px 18px 0 0;color:#fff;font-size:20px;font-weight:700;"><img src="cid:portal-logo" width="26" height="26" alt="" style="display:inline-block;margin-right:10px;vertical-align:-7px;border:0;">Portal de Proveedores</td></tr>
                  <tr><td style="background:#fff;padding:34px 40px;border-radius:0 0 18px 18px;">
                    <h1 style="margin:0 0 14px;font-size:24px;line-height:1.25;">{title}</h1>
                    <p style="margin:0 0 20px;font-size:16px;line-height:1.6;color:#4a5568;">{description}</p>
                    <table role="presentation" cellpadding="0" cellspacing="0" width="100%" style="margin:0 0 20px;padding:12px 18px;background:#f4f8fe;border:1px solid #dce5f2;border-radius:12px;">{rows}</table>
                    <p style="margin:0;font-size:14px;line-height:1.6;color:#5a6477;">{note}</p>
                  </td></tr>
                  <tr><td style="padding:16px 10px 0;text-align:center;font-size:12px;color:#5a6477;">Este es un mensaje automático, por favor no respondas.</td></tr>
                </table>
              </div>
            </body>
            </html>
            """;
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
