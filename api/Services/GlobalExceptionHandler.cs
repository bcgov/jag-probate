using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Probate.Api.Helpers.Exceptions;

namespace Probate.Api.Services
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken
        )
        {
            if (exception is ChefsApiException chefsEx)
            {
                var statusCode = (int)chefsEx.StatusCode;
                if (statusCode < 400 || statusCode >= 600)
                    statusCode = StatusCodes.Status502BadGateway;

                _logger.LogWarning(chefsEx, "CHEFS API error: {Message}", chefsEx.Message);

                var problemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = "CHEFS API error",
                    Detail = chefsEx.Message,
                };

                httpContext.Response.StatusCode = statusCode;
                await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
                return true;
            }

            _logger.LogError(exception, "An unhandled exception occurred");

            var problemDetails500 = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An error occurred while processing your request.",
                Detail = exception.Message,
            };

            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await httpContext.Response.WriteAsJsonAsync(problemDetails500, cancellationToken);

            return true;
        }
    }
}
