using System;
using System.Collections.Generic;
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
/// Form ID is passed by the frontend and must match the configured Chefs:FormId.
/// </summary>
[Route("api/chefs/[controller]")]
[ApiController]
[Authorize]
public class ApplicationsController : ControllerBase
{
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
    /// <param name="formId">CHEFS form UUID (must match Chefs:FormId in config).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">List of applications.</response>
    /// <response code="400">FormId missing, not allowed, or Chefs:FormId not configured.</response>
    /// <response code="502">CHEFS API unreachable or returned an error.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ApplicationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<IReadOnlyList<ApplicationDto>>> GetApplications(
        [FromQuery] string formId,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(formId))
        {
            return BadRequest(new { message = "formId is required." });
        }

        try
        {
            var applications = await _chefsApplicationService.GetApplicationsAsync(formId, cancellationToken);
            return Ok(applications);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogInformation(ex, "Invalid request for CHEFS applications: {Message}", ex.Message);
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
