using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Probate.Api.Models;
using Probate.Api.Services;

namespace Probate.Api.Controllers;

/// <summary>
/// Exposes CHEFS applications (current and previous submissions) for the dashboard.
/// The frontend passes a logical form key; the backend resolves it to the actual CHEFS form GUID (never exposed to callers).
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ChefsController : ControllerBase
{
    /// <summary>
    /// Allowlist for form key values: alphanumeric, hyphens, underscores only.
    /// Prevents log injection from unsanitised user input reaching log sinks or error messages.
    /// </summary>
    private static readonly Regex FormKeyPattern = new(@"^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);

    private readonly IChefsApplicationService _chefsApplicationService;

    public ChefsController(IChefsApplicationService chefsApplicationService)
    {
        _chefsApplicationService = chefsApplicationService;
    }

    /// <summary>
    /// Gets all applications (submissions) for the given CHEFS form.
    /// </summary>
    /// <param name="formKey">Logical form key (e.g. "probate"). Resolved to the actual CHEFS form GUID server-side via Chefs:Forms config.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">List of applications.</response>
    /// <response code="400">formKey missing, invalid characters, or not a recognised form key.</response>
    /// <response code="502">CHEFS API unreachable or returned an error.</response>
    [HttpGet("Applications")]
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

        var applications = await _chefsApplicationService.GetApplicationsAsync(
            formKey,
            cancellationToken
        );
        return Ok(applications);
    }

    /// <summary>
    /// Gets a short-lived auth token for the CHEFS web component.
    /// The token is used with the X-Chefs-Gateway-Token header for client-side form embedding.
    /// </summary>
    /// <param name="formKey">Logical form key (e.g. "legal"). Resolved to the actual CHEFS form GUID server-side via Chefs:Forms config.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Auth token and form ID.</response>
    /// <response code="400">formKey missing, invalid characters, or not a recognised form key.</response>
    /// <response code="502">CHEFS API unreachable or returned an error.</response>
    [HttpGet("Auth")]
    [ProducesResponseType(typeof(ChefsAuthTokenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ChefsAuthTokenDto>> GetAuthToken(
        [FromQuery] string formKey,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(formKey))
            return BadRequest(new { message = "formKey is required." });

        if (!FormKeyPattern.IsMatch(formKey))
            return BadRequest(new { message = "formKey contains invalid characters." });

        var tokenResponse = await _chefsApplicationService.GetAuthTokenAsync(
            formKey,
            cancellationToken
        );
        return Ok(tokenResponse);
    }
}
