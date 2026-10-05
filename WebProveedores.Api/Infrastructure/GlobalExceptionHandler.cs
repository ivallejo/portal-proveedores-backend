using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WebProveedores.Application.Abstractions;
using WebProveedores.Application.Documents;

namespace WebProveedores.Api.Infrastructure;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var statusCode = exception switch
        {
            DocumentRejectedException => StatusCodes.Status422UnprocessableEntity,
            ServiceUnavailableException => StatusCodes.Status503ServiceUnavailable,
            ArgumentException => StatusCodes.Status400BadRequest,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            // Las InvalidOperationException de librerías (por ejemplo EF Core) son errores internos, no de negocio.
            InvalidOperationException when exception.Source?.StartsWith("Microsoft.", StringComparison.Ordinal) != true => StatusCodes.Status409Conflict,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status500InternalServerError
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception processing {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        else
            logger.LogWarning(exception, "Application error processing {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode == StatusCodes.Status500InternalServerError ? "Ocurrió un error inesperado." : exception.Message,
            Detail = statusCode == StatusCodes.Status500InternalServerError ? "Intenta nuevamente o contacta al administrador." : exception.Message,
            Instance = httpContext.Request.Path
        };
        // El frontend muestra el campo «message», igual que en las demás respuestas de error.
        problem.Extensions["message"] = problem.Detail;
        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
