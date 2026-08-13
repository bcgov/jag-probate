using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Probate.Api.Models;
using Probate.Api.Services;

namespace Probate.Api.Controllers
{
    [Route("api/submissions/{publicId}/steps")]
    [ApiController]
    [Authorize]
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
        /// Gets a single step's data for hydration.
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
                formId,
                cancellationToken
            );

            if (result == null)
                return NotFound(new { message = $"Step data not found for {formId}." });

            return Ok(result);
        }

        /// <summary>
        /// Gets all step data for a submission.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<StepDataResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<StepDataResponseDto>>> GetAllStepData(
            Guid publicId,
            CancellationToken cancellationToken = default
        )
        {
            var result = await _stepDataService.GetAllStepDataAsync(publicId, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Returns the compiled (merged) JSON data from all steps.
        /// Used for PDF generation and final submission review.
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
