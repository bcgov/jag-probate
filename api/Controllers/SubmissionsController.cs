using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Probate.Api.Infrastructure.Authentication;
using Probate.Api.Models;
using Probate.Api.Services;

namespace Probate.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [SubmissionOwnership]
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
        /// Creates a draft submission record for the step-based wizard flow.
        /// Stores only the authoritative user ID, never the deprecated username.
        /// </summary>
        [HttpPost("draft")]
        [ProducesResponseType(typeof(SubmissionResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<SubmissionResponseDto>> CreateDraftSubmission(
            CancellationToken cancellationToken = default
        )
        {
            var owner = SubmissionOwner.FromPrincipal(User);
            if (owner == null)
                return Unauthorized(new { message = "Unable to identify current user." });

            var result = await _submissionService.CreateDraftSubmissionAsync(
                owner,
                cancellationToken
            );
            return CreatedAtAction(nameof(GetSubmission), new { id = result.PublicId }, result);
        }

        /// <summary>
        /// Reads and atomically claims an owned live submission by public ID.
        /// Username fallback applies only to rows with a null authoritative user ID.
        /// </summary>
        [HttpGet("{id}")]
        [SubmissionOwnership("id")]
        [ProducesResponseType(typeof(SubmissionResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SubmissionResponseDto>> GetSubmission(
            Guid id,
            CancellationToken cancellationToken = default
        )
        {
            var result = await _submissionService.GetSubmissionByIdAsync(
                id,
                SubmissionOwner.FromPrincipal(User)!,
                cancellationToken
            );
            if (result is null)
                return NotFound(new { message = $"Submission {id} not found." });
            return Ok(result);
        }

        /// <summary>
        /// Lists and claims live rows owned by user ID or the null-ID legacy fallback.
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
            var owner = SubmissionOwner.FromPrincipal(User);
            if (owner == null)
                return Unauthorized(new { message = "Unable to identify current user." });

            var submissions = await _submissionService.GetSubmissionsByUserAsync(
                owner,
                cancellationToken
            );
            _logger.LogInformation(
                "Fetched {number} of submissions for current user",
                submissions.Count
            );
            return Ok(submissions);
        }

        /// <summary>
        /// Compiles all step data into a full data model, stores it in the
        /// submission record, and marks the submission as ready to print.
        /// The submission remains editable.
        /// Atomically claims legacy ownership before reading steps or writing submission data.
        /// </summary>
        [HttpPost("{id}/submit")]
        [SubmissionOwnership("id")]
        [ProducesResponseType(typeof(SubmissionResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SubmissionResponseDto>> SubmitApplication(
            Guid id,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
                var compiledSubmission = await _stepDataService.GetCompiledSubmissionDataAsync(
                    id,
                    SubmissionOwner.FromPrincipal(User)!,
                    cancellationToken
                );

                var result = await _submissionService.FinalizeSubmissionAsync(
                    compiledSubmission.SubmissionId,
                    SubmissionOwner.FromPrincipal(User)!,
                    compiledSubmission.Data,
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
        /// Claims and soft deletes an owned row; preserves the deprecated legacy username.
        /// </summary>
        [HttpDelete("{id}")]
        [SubmissionOwnership("id")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteSubmission(
            Guid id,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
                await _submissionService.DeleteSubmissionAsync(
                    id,
                    SubmissionOwner.FromPrincipal(User)!,
                    cancellationToken
                );
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = $"Submission {id} not found." });
            }
        }
    }
}
