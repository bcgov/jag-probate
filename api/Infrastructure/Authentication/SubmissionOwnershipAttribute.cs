using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Probate.Api.Services;
using Probate.Db.Models;

namespace Probate.Api.Infrastructure.Authentication;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SubmissionOwnershipAttribute : TypeFilterAttribute
{
    public SubmissionOwnershipAttribute(string idParameter = "")
        : base(typeof(SubmissionOwnershipFilter))
    {
        Arguments = new object[] { idParameter };
        // Identity/resource checks must precede ApiController's automatic model validation.
        Order = -3000;
    }
}

public sealed class SubmissionOwnershipFilter : IAsyncActionFilter
{
    private readonly ProbateDbContext _db;
    private readonly string _idParameter;

    public SubmissionOwnershipFilter(ProbateDbContext db, string idParameter)
    {
        _db = db;
        _idParameter = idParameter;
    }

    /// <summary>
    /// Requires an authoritative user ID on every submission/step endpoint.
    /// An optional route ID is checked in SQL using the shared ownership predicate.
    /// Actual reads and writes perform the guarded legacy claim in the service.
    /// </summary>
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        var owner = SubmissionOwner.FromPrincipal(context.HttpContext.User);
        if (owner == null)
        {
            context.Result = new UnauthorizedObjectResult(
                new { message = "Unable to identify current user." }
            );
            return;
        }

        if (_idParameter.Length == 0)
        {
            await next();
            return;
        }

        // Use the same response for missing and foreign submissions to avoid disclosing existence.
        if (
            !context.ActionArguments.TryGetValue(_idParameter, out var value)
            || value is not Guid publicId
            || !await _db
                .Submissions.Where(SubmissionOwnership.OwnedBy(owner))
                .AnyAsync(
                    s => s.PublicId == publicId && s.DeletedAt == null,
                    context.HttpContext.RequestAborted
                )
        )
        {
            context.Result = new NotFoundObjectResult(new { message = "Submission not found." });
            return;
        }

        await next();
    }
}
