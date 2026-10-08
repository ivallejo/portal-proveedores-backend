namespace WebProveedores.Application.Common.Exceptions;

/// <summary>Sin permiso para la acción o el recurso (403).</summary>
public sealed class ForbiddenException(string message) : AppException(message);
