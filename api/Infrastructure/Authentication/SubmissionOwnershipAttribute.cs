using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Probate.Db.Models;

namespace Probate.Api.Infrastructure.Authentication;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SubmissionOwnershipAttribute : TypeFilterAttribute
{
    public SubmissionOwnershipAttribute(string idParameter)
        : base(typeof(SubmissionOwnershipFilter))
    {
        Arguments = new object[] { idParameter };
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

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        var username = context.HttpContext.User.FindFirstValue("preferred_username");
        if (string.IsNullOrWhiteSpace(username))
        {
            context.Result = new UnauthorizedObjectResult(
                new { message = "Unable to identify current user." }
            );
            return;
        }

        // Use the same response for missing and foreign submissions to avoid disclosing existence.
        if (
            !context.ActionArguments.TryGetValue(_idParameter, out var value)
            || value is not Guid publicId
            || !await _db.Submissions.AnyAsync(
                s => s.PublicId == publicId && s.CreatedBy == username && s.DeletedAt == null,
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
