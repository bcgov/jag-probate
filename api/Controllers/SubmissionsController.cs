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

            if (string.IsNullOrWhiteSpace(username))
                return Unauthorized(new { message = "Unable to identify current user." });

            var submissions = await _submissionService.GetSubmissionsByUserAsync(
                username,
                cancellationToken
            );
            return Ok(submissions);
        }
    }
}
