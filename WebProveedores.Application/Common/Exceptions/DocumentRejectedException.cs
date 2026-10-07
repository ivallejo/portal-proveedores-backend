namespace WebProveedores.Application.Common.Exceptions;

/// <summary>El documento no superó una validación de negocio (SAP, SUNAT o duplicidad).</summary>
public sealed class DocumentRejectedException(string message) : Exception(message);
