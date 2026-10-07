namespace WebProveedores.Application.Common.Exceptions;

/// <summary>Un servicio externo (SAP, correo) no respondió. Se informa al usuario con 503 para que reintente.</summary>
public sealed class ServiceUnavailableException(string message, Exception? innerException = null) : Exception(message, innerException);
