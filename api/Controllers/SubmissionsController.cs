using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Probate.Api.Models;
using Probate.Api.Services;

namespace Probate.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SubmissionsController : ControllerBase
    {
        private readonly ISubmissionService _submissionService;
        private readonly ILogger<SubmissionsController> _logger;

        public SubmissionsController(
            ISubmissionService submissionService,
            ILogger<SubmissionsController> logger
        )
        {
            _submissionService = submissionService;
            _logger = logger;
        }

        /// <summary>
        /// Creates a local submission record after a successful CHEFS form submission.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(SubmissionResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<SubmissionResponseDto>> CreateSubmission(
            [FromBody] CreateSubmissionDto dto,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrWhiteSpace(dto.ChefsSubmissionId))
                return BadRequest(new { message = "ChefsSubmissionId is required." });

            var result = await _submissionService.CreateSubmissionAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(CreateSubmission), new { id = result.Id }, result);
        }

        /// <summary>
        /// Gets all submissions for the currently authenticated user.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(
            typeof(IReadOnlyList<SubmissionResponseDto>),
            StatusCodes.Status200OK
        )]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<IReadOnlyList<SubmissionResponseDto>>> GetSubmissions(
            CancellationToken cancellationToken = default
        )
        {
            var username = User.FindFirstValue("preferred_username");
            _logger.LogInformation("Fetching submissions for username: {Username}", username);
            if (string.IsNullOrWhiteSpace(username))
                return Unauthorized(new { message = "Unable to identify current user." });

            var submissions = await _submissionService.GetSubmissionsByUserAsync(
                username,
                cancellationToken
            );
            _logger.LogInformation(
                "Fetched {number} of submissions for username: {Username}",
                submissions.Count,
                username
            );
            return Ok(submissions);
        }

        /// <summary>
        /// Creates or updates a local submission record by ChefsSubmissionId.
        /// If a record with the same ChefsSubmissionId exists, it is updated.
        /// Otherwise a new record is created.
        /// </summary>
        [HttpPost("upsert")]
        [ProducesResponseType(typeof(SubmissionResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<SubmissionResponseDto>> UpsertSubmission(
            [FromBody] CreateSubmissionDto dto,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrWhiteSpace(dto.ChefsSubmissionId))
                return BadRequest(new { message = "ChefsSubmissionId is required." });

            var result = await _submissionService.UpsertSubmissionAsync(dto, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Soft deletes a local submission record and deletes it from CHEFS.
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteSubmission(
            int id,
            CancellationToken cancellationToken = default
        )
        {
            await _submissionService.DeleteSubmissionAsync(id, cancellationToken);
            return NoContent();
        }
    }
}
