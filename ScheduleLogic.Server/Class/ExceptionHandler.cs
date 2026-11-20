using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace ScheduleLogic.Server.Class
{
    public class ExceptionHandler : IExceptionHandler
    {
        private const int DatabaseRetryAfterSeconds = 10;

        private readonly ILogger<ExceptionHandler> _logger;

        public ExceptionHandler(ILogger<ExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
            {
                _logger.LogDebug("Request aborted by the client: {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
                return true;
            }

            ProblemDetails problem;
            if (IsDatabaseUnavailable(exception))
            {
                _logger.LogError("Database unavailable during {Method} {Path}: {Message}",
                    httpContext.Request.Method, httpContext.Request.Path, Innermost(exception).Message);
                httpContext.Response.Headers.RetryAfter = DatabaseRetryAfterSeconds.ToString();
                problem = new ProblemDetails
                {
                    Title = "Service temporarily unavailable",
                    Status = StatusCodes.Status503ServiceUnavailable
                };
            }
            else
            {
                _logger.LogError(exception, "Unhandled exception during {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
                problem = exception switch
                {
                    KeyNotFoundException => new ProblemDetails
                    {
                        Title = "Resource not found",
                        Status = StatusCodes.Status404NotFound
                    },

                    ArgumentException => new ProblemDetails
                    {
                        Title = "Invalid request",
                        Status = StatusCodes.Status400BadRequest
                    },

                    _ => new ProblemDetails
                    {
                        Title = "Internal server error",
                        Status = StatusCodes.Status500InternalServerError
                    }
                };
            }

            problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
            httpContext.Response.StatusCode = problem.Status!.Value;
            await httpContext.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json", cancellationToken);
            return true;
        }

        private static bool IsDatabaseUnavailable(Exception exception)
        {
            for (var e = exception; e != null; e = e.InnerException)
            {
                switch (e)
                {
                    case PostgresException pg:
                        return pg.SqlState.StartsWith("08") || pg.SqlState.StartsWith("53")
                            || pg.SqlState.StartsWith("57P") || pg.SqlState == PostgresErrorCodes.InvalidPassword
                            || pg.SqlState == PostgresErrorCodes.InvalidAuthorizationSpecification;
                    case NpgsqlException:
                        return true;
                }
            }
            return false;
        }

        private static Exception Innermost(Exception exception)
        {
            while (exception.InnerException != null) exception = exception.InnerException;
            return exception;
        }
    }
}
