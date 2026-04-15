using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
            if (exception is OperationCanceledException)
            {
                httpContext.Response.StatusCode = 499; // Client Closed Request
                return true;
            }

            var problemDetails = exception switch
            {
                ChefsApiException ex => HandleChefsApiException(ex, httpContext),
                KeyNotFoundException ex => HandleKeyNotFoundException(ex, httpContext),
                InvalidOperationException ex => HandleInvalidOperationException(ex, httpContext),
                DbUpdateConcurrencyException ex => HandleConcurrencyException(ex, httpContext),
                _ => HandleUnknownException(exception, httpContext),
            };

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
            return true;
        }

        private ProblemDetails HandleChefsApiException(ChefsApiException ex, HttpContext httpContext)
        {
            var statusCode = (int)ex.StatusCode;
            if (statusCode < 400 || statusCode >= 600)
                statusCode = StatusCodes.Status502BadGateway;

            _logger.LogWarning(ex, "CHEFS API error: {Message}", ex.Message);
            httpContext.Response.StatusCode = statusCode;

            return new ProblemDetails
            {
                Status = statusCode,
                Title = "CHEFS API error",
                Detail = ex.Message,
            };
        }

        private ProblemDetails HandleKeyNotFoundException(KeyNotFoundException ex, HttpContext httpContext)
        {
            _logger.LogInformation(ex, "Resource not found: {Message}", ex.Message);
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

            return new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource not found.",
                Detail = ex.Message,
            };
        }

        private ProblemDetails HandleInvalidOperationException(InvalidOperationException ex, HttpContext httpContext)
        {
            _logger.LogInformation(ex, "Invalid operation: {Message}", ex.Message);
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

            return new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid request.",
                Detail = ex.Message,
            };
        }

        private ProblemDetails HandleConcurrencyException(DbUpdateConcurrencyException ex, HttpContext httpContext)
        {
            _logger.LogWarning(ex, "Concurrency conflict: {Message}", ex.Message);
            httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

            return new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict.",
                Detail = "The resource was modified by another request. Please retry.",
            };
        }

        private ProblemDetails HandleUnknownException(Exception ex, HttpContext httpContext)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

            return new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An error occurred while processing your request.",
                Detail = ex.Message,
            };
        }
    }
}
