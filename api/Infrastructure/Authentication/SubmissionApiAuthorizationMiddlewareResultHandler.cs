using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

namespace Probate.Api.Infrastructure.Authentication;

/// <summary>
/// Returns 401 for unauthenticated submission API requests without changing the
/// OpenID Connect challenge used by the explicit login endpoint or other routes.
/// </summary>
public sealed class SubmissionApiAuthorizationMiddlewareResultHandler
    : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    /// <inheritdoc />
    public Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult
    )
    {
        if (
            authorizeResult.Challenged
            && context.Request.Path.StartsWithSegments(
                "/api/submissions",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        return _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
