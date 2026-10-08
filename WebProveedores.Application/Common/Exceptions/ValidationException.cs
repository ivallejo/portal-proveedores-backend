namespace WebProveedores.Application.Common.Exceptions;

/// <summary>Datos inválidos (400).</summary>
public sealed class ValidationException(string message) : AppException(message);
