namespace WebProveedores.Application.Ports.Outbound.Notifications.Models;

/// <summary>Archivo adjunto a un correo (por ejemplo, los archivos de un documento observado).</summary>
public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);
