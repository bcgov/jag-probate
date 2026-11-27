using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Probate.Api.Infrastructure.Authentication;
using Probate.Api.Models;
using Probate.Api.Services;

namespace Probate.Api.Controllers
{
    [Route("api/submissions/{publicId}/steps")]
    [ApiController]
    [Authorize]
    [SubmissionOwnership("publicId")]
    public class StepDataController : ControllerBase
    {
        private readonly IStepDataService _stepDataService;

        public StepDataController(IStepDataService stepDataService)
        {
            _stepDataService = stepDataService;
        }

        /// <summary>
        /// Upserts a single step's data for a submission.
        /// Used by auto-save and manual save — only touches the targeted step.
        /// Claims the owned legacy parent before writing; user-ID ownership is authoritative.
        /// </summary>
        [HttpPut("{formId}")]
        [ProducesResponseType(typeof(StepDataResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<StepDataResponseDto>> UpsertStepData(
            Guid publicId,
            string formId,
            [FromBody] UpsertStepDataDto dto,
            CancellationToken cancellationToken = default
        )
        {
            dto.FormId = formId;

            try
            {
                var result = await _stepDataService.UpsertStepDataAsync(
                    publicId,
                    SubmissionOwner.FromPrincipal(User)!,
                    dto,
                    cancellationToken
                );
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = $"Submission {publicId} not found." });
            }
        }

        /// <summary>
        /// Claims the owned parent and reads one step; username fallback is limited to null owner IDs.
        /// </summary>
        [HttpGet("{formId}")]
        [ProducesResponseType(typeof(StepDataResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<StepDataResponseDto>> GetStepData(
            Guid publicId,
            string formId,
            CancellationToken cancellationToken = default
        )
        {
            var result = await _stepDataService.GetStepDataAsync(
                publicId,
                SubmissionOwner.FromPrincipal(User)!,
                formId,
                cancellationToken
            );

            if (result == null)
                return NotFound(new { message = $"Step data not found for {formId}." });

            return Ok(result);
        }

        /// <summary>
        /// Claims the owned parent and reads its steps; no user ID is exposed in responses.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<StepDataResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<StepDataResponseDto>>> GetAllStepData(
            Guid publicId,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
                var result = await _stepDataService.GetAllStepDataAsync(
                    publicId,
                    SubmissionOwner.FromPrincipal(User)!,
                    cancellationToken
                );
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = $"Submission {publicId} not found." });
            }
        }

        /// <summary>
        /// Returns the compiled (merged) JSON data from all steps.
        /// Used for PDF generation and final submission review.
        /// Claims legacy ownership atomically before reading the parent's data.
        /// </summary>
        [HttpGet("compiled")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<string>> GetCompiledData(
            Guid publicId,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
                var result = await _stepDataService.GetCompiledDataAsync(
                    publicId,
                    SubmissionOwner.FromPrincipal(User)!,
                    cancellationToken
                );
                return Content(result, "application/json");
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = $"Submission {publicId} not found." });
            }
        }
    }
}
