using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Probate.Api.Helpers.Exceptions;
using Probate.Api.Models;
using Probate.Api.Services;

namespace Probate.Api.Controllers;

/// <summary>
/// Exposes CHEFS applications (current and previous submissions) for the dashboard.
/// The frontend passes a logical form key; the backend resolves it to the actual CHEFS form GUID (never exposed to callers).
/// </summary>
[Route("api/chefs/[controller]")]
[ApiController]
[Authorize]
public class ApplicationsController : ControllerBase
{
    /// <summary>
    /// Allowlist for form key values: alphanumeric, hyphens, underscores only.
    /// Prevents log injection from unsanitised user input reaching log sinks or error messages.
    /// </summary>
    private static readonly Regex FormKeyPattern = new(@"^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);

    private readonly IChefsApplicationService _chefsApplicationService;
    private readonly ILogger<ApplicationsController> _logger;

    public ApplicationsController(
        IChefsApplicationService chefsApplicationService,
        ILogger<ApplicationsController> logger
    )
    {
        _chefsApplicationService = chefsApplicationService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all applications (submissions) for the given CHEFS form.
    /// </summary>
    /// <param name="formKey">Logical form key (e.g. "probate"). Resolved to the actual CHEFS form GUID server-side via Chefs:Forms config.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">List of applications.</response>
    /// <response code="400">formKey missing, invalid characters, or not a recognised form key.</response>
    /// <response code="502">CHEFS API unreachable or returned an error.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ApplicationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<IReadOnlyList<ApplicationDto>>> GetApplications(
        [FromQuery] string formKey,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(formKey))
            return BadRequest(new { message = "formKey is required." });

        if (!FormKeyPattern.IsMatch(formKey))
            return BadRequest(new { message = "formKey contains invalid characters." });

        try
        {
            var applications = await _chefsApplicationService.GetApplicationsAsync(
                formKey,
                cancellationToken
            );
            return Ok(applications);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogInformation(
                ex,
                "Invalid request for CHEFS applications: {Message}",
                ex.Message
            );
            return BadRequest(new { message = ex.Message });
        }
        catch (ChefsApiException ex)
        {
            var statusCode = (int)ex.StatusCode;
            if (statusCode >= 400 && statusCode < 600)
            {
                return Problem(
                    detail: ex.Message,
                    title: "CHEFS API error",
                    statusCode: statusCode
                );
            }

            return Problem(
                detail: ex.Message,
                title: "CHEFS API error",
                statusCode: StatusCodes.Status502BadGateway
            );
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499); // Client Closed Request
        }
    }
}
