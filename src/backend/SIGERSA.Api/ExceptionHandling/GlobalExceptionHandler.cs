using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGERSA.Domain.Exceptions;

namespace SIGERSA.Api.ExceptionHandling;

public sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Error de validación"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Solicitud no válida"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "No autorizado"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Acceso denegado"),
            OptimisticConcurrencyException => (StatusCodes.Status409Conflict, "Conflicto de concurrencia"),
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } =>
                (StatusCodes.Status409Conflict, "Registro duplicado"),
            EmailDeliveryException => (StatusCodes.Status503ServiceUnavailable, "Servicio de correo no disponible"),
            InvalidOperationException => (StatusCodes.Status409Conflict, "Operación no permitida"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Recurso no encontrado"),
            _ => (StatusCodes.Status500InternalServerError, "Error interno del servidor")
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandledException(logger, exception, httpContext.TraceIdentifier);
        }
        else
        {
            LogRejectedRequest(logger, status, httpContext.TraceIdentifier);
        }

        httpContext.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = $"https://httpstatuses.com/{status}",
            Detail = exception switch
            {
                PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } =>
                    "Ya existe un registro con el mismo correo o identificación.",
                _ when status == StatusCodes.Status500InternalServerError => "La solicitud no pudo completarse.",
                _ => exception.Message
            },
            Instance = httpContext.Request.Path
        };

        problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        if (exception is ValidationException validationException)
        {
            problem.Extensions["errors"] = validationException.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).ToArray());
        }

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Error,
        Message = "Ocurrió un error no controlado. TraceId: {TraceId}")]
    private static partial void LogUnhandledException(
        ILogger logger,
        Exception exception,
        string traceId);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Solicitud rechazada con estado {StatusCode}. TraceId: {TraceId}")]
    private static partial void LogRejectedRequest(
        ILogger logger,
        int statusCode,
        string traceId);
}
