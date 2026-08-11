using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
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
        private readonly IStepDataService _stepDataService;
        private readonly ILogger<SubmissionsController> _logger;

        public SubmissionsController(
            ISubmissionService submissionService,
            IStepDataService stepDataService,
            ILogger<SubmissionsController> logger
        )
        {
            _submissionService = submissionService;
            _stepDataService = stepDataService;
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
            return CreatedAtAction(nameof(CreateSubmission), new { id = result.PublicId }, result);
        }

        /// <summary>
        /// Creates a draft submission record for the step-based wizard flow.
        /// No CHEFS submission ID is required — CHEFS is only contacted on final submit.
        /// </summary>
        [HttpPost("draft")]
        [ProducesResponseType(typeof(SubmissionResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<SubmissionResponseDto>> CreateDraftSubmission(
            CancellationToken cancellationToken = default
        )
        {
            var username = User.FindFirstValue("preferred_username");
            if (string.IsNullOrWhiteSpace(username))
                return Unauthorized(new { message = "Unable to identify current user." });

            var result = await _submissionService.CreateDraftSubmissionAsync(
                username, cancellationToken
            );
            return CreatedAtAction(nameof(GetSubmission), new { id = result.PublicId }, result);
        }

        /// <summary>
        /// Gets a single submission by its DB id.
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(SubmissionResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SubmissionResponseDto>> GetSubmission(
            Guid id,
            CancellationToken cancellationToken = default
        )
        {
            var result = await _submissionService.GetSubmissionByIdAsync(id, cancellationToken);
            if (result is null)
                return NotFound(new { message = $"Submission {id} not found." });
            return Ok(result);
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
        /// Compiles all step data into a full data model, stores it in the
        /// submission record, and marks the submission as submitted.
        /// </summary>
        [HttpPost("{id}/submit")]
        [ProducesResponseType(typeof(SubmissionResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SubmissionResponseDto>> SubmitApplication(
            Guid id,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
                var compiledData = await _stepDataService.GetCompiledDataAsync(
                    id,
                    cancellationToken
                );

                var result = await _submissionService.FinalizeSubmissionAsync(
                    id,
                    compiledData,
                    cancellationToken
                );

                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = $"Submission {id} not found." });
            }
        }

        /// <summary>
        /// Soft deletes a local submission record and deletes it from CHEFS.
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteSubmission(
            Guid id,
            CancellationToken cancellationToken = default
        )
        {
            await _submissionService.DeleteSubmissionAsync(id, cancellationToken);
            return NoContent();
        }
    }
}
