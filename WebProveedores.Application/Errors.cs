namespace WebProveedores.Application;

/// <summary>
/// Error esperado de un caso de uso, con un mensaje apto para el usuario. La API traduce cada tipo a su código HTTP;
/// cualquier otra excepción es un error interno (500) y su detalle no se muestra.
/// </summary>
public abstract class AppException(string message, Exception? innerException = null) : Exception(message, innerException);

/// <summary>Datos inválidos (400).</summary>
public sealed class ValidationException(string message) : AppException(message);

/// <summary>Sin permiso para la acción o el recurso (403).</summary>
public sealed class ForbiddenException(string message) : AppException(message);

/// <summary>El recurso no existe o no es visible para el usuario (404).</summary>
public sealed class NotFoundException(string message) : AppException(message);

/// <summary>La acción choca con el estado actual: duplicados, reglas de administración (409).</summary>
public sealed class ConflictException(string message) : AppException(message);
