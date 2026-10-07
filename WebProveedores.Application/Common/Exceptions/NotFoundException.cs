namespace WebProveedores.Application.Common.Exceptions;

/// <summary>El recurso no existe o no es visible para el usuario (404).</summary>
public sealed class NotFoundException(string message) : AppException(message);
