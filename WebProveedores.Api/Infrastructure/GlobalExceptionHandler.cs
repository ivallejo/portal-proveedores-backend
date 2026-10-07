using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WebProveedores.Application;
using WebProveedores.Application.Abstractions;
using WebProveedores.Application.Auth;
using WebProveedores.Application.Documents;
using WebProveedores.Domain.Common;

namespace WebProveedores.Api.Infrastructure;

/// <summary>
/// Traduce los errores esperados de la aplicación y del dominio a su código HTTP con un mensaje para el usuario.
/// Cualquier otra excepción es un error interno: 500 con un mensaje genérico, sin detalles.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var statusCode = exception switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
            ForbiddenException => StatusCodes.Status403Forbidden,
            NotFoundException => StatusCodes.Status404NotFound,
            ConflictException or DomainRuleException => StatusCodes.Status409Conflict,
            DocumentRejectedException => StatusCodes.Status422UnprocessableEntity,
            AccountLockedException => StatusCodes.Status429TooManyRequests,
            ServiceUnavailableException => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError,
        };

        var internalError = statusCode == StatusCodes.Status500InternalServerError;
        if (internalError)
            logger.LogError(exception, "Unhandled exception processing {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        else
            logger.LogWarning("{Status} en {Method} {Path}: {Message}", statusCode, httpContext.Request.Method, httpContext.Request.Path, exception.Message);

        var message = exception switch
        {
            _ when internalError => "Intenta nuevamente o contacta al administrador.",
            AccountLockedException locked => $"Demasiados intentos fallidos. Vuelve a intentarlo en {Math.Max(1, (int)Math.Ceiling(locked.RetryAfter.TotalMinutes))} minuto(s) o recupera tu contraseña.",
            _ => exception.Message,
        };
        if (exception is AccountLockedException lockout)
            httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(lockout.RetryAfter.TotalSeconds)).ToString();

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = internalError ? "Ocurrió un error inesperado." : message,
            Detail = message,
            Instance = httpContext.Request.Path,
        };
        // El frontend muestra el campo «message», igual que en las demás respuestas de error.
        problem.Extensions["message"] = message;
        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
