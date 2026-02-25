using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ScheduleLogic.Server.Class
{
    public class ExceptionHandler : IExceptionHandler
    {
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
            _logger.LogError(exception, "Unhandled exception");

            var problem = exception switch
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

            httpContext.Response.StatusCode = problem.Status.Value;

            await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

            return true;
        }
    }
}
