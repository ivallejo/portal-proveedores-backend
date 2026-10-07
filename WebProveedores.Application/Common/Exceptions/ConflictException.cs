namespace WebProveedores.Application.Common.Exceptions;

/// <summary>La acción choca con el estado actual: duplicados, reglas de administración (409).</summary>
public sealed class ConflictException(string message) : AppException(message);
