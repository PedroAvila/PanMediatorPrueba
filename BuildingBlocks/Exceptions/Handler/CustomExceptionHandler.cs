using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Exceptions.Handler;

public class CustomExceptionHandler(ILogger<CustomExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        // Cada marcador con nombre viaja como customDimensions a Application Insights, que es donde
        // se consultan. Sin gymId, un error en Azure no dice de qué gimnasio es.
        logger.LogError(
            exception,
            "Error Message: {exceptionMessage}, Time of occurrence {time}, Request {method} {path}, TraceId {traceId}",
            exception.Message,
            DateTime.UtcNow,
            context.Request.Method,
            context.Request.Path,
            context.TraceIdentifier
        );

        (string Detail, string Title, int StatusCode) = exception switch
        {
            ValidationException => (
                exception.Message,
                exception.GetType().Name,
                StatusCodes.Status400BadRequest
            ),
            BusinessException customException => (
                customException.Message,
                // exception.GetType().Name,
                "Error de negocio",
                (int)customException.Status
            ),
            _ => (
                // exception.Message,
                // exception.GetType().Name,
                "Ocurrió un error inesperado en nuestro sistema. Por favor, intente más tarde.",
                "Internal Server Error",
                StatusCodes.Status500InternalServerError
            ),
        };

        context.Response.StatusCode = StatusCode;

        var problemDetails = new ProblemDetails
        {
            Title = Title,
            Detail = Detail,
            Status = StatusCode,
            Instance = context.Request.Path,
        };

        problemDetails.Extensions.Add("traceId", context.TraceIdentifier);
        if (exception is ValidationException validationException)
            problemDetails.Extensions.Add("ValidationErrors", validationException.Errors);

        await context.Response.WriteAsJsonAsync(
            problemDetails,
            cancellationToken: cancellationToken
        );
        return true;
    }

    /// <summary>
    /// Devuelve <c>null</c> —y no cadena vacía— cuando el claim falta o viene vacío, para que la
    /// propiedad no ensucie la consulta. Pasa en dos casos normales: SUPERADMIN, que no pertenece a
    /// ningún gimnasio, y los errores anteriores a la autenticación (login, token inválido).
    /// </summary>
    private static string? GetClaim(HttpContext context, string claimType)
    {
        var value = context.User?.FindFirst(claimType)?.Value;
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
