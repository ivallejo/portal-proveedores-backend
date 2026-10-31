namespace WebProveedores.Application.Payments.Responses;

/// <summary>Archivo descargable: nombre sugerido y contenido.</summary>
public sealed record ExportFileResponse(string FileName, byte[] Content);
