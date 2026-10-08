namespace WebProveedores.Application.Common.Exceptions;

/// <summary>
/// Error esperado de un caso de uso, con un mensaje apto para el usuario. La API traduce cada tipo a su código HTTP;
/// cualquier otra excepción es un error interno (500) y su detalle no se muestra.
/// </summary>
public abstract class AppException(string message, Exception? innerException = null) : Exception(message, innerException);
