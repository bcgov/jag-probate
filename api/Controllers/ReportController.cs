using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Probate.Api.Infrastructure.CDogs;
using Probate.Api.Models;
using Probate.Api.Models.CDogs;
using Probate.Api.Services;

namespace Probate.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportController : ControllerBase
{
    private static readonly Regex SafeKeyPattern = new(@"^[a-zA-Z0-9\-]+$", RegexOptions.Compiled);

    private readonly ICDogsDelegate _cdogsDelegate;
    private readonly ITemplateService _templateService;

    public ReportController(
        ICDogsDelegate cdogsDelegate,
        ITemplateService templateService
    )
    {
        _cdogsDelegate = cdogsDelegate;
        _templateService = templateService;
    }

    /// <summary>
    /// Generates a PDF from the provided submission data and a named template.
    /// Streams the PDF bytes directly to the caller as application/pdf.
    /// </summary>
    [HttpPost("generate-from-submission")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateFromSubmission(
        [FromBody] GenerateReportFromSubmissionRequest request,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(request.TemplateKey))
            return BadRequest(new { message = "templateKey is required." });

        if (!SafeKeyPattern.IsMatch(request.TemplateKey))
            return BadRequest(new { message = "templateKey contains invalid characters." });

        var templateBase64 = _templateService.GetTemplateBase64(request.TemplateKey);

        var cdogsRequest = new CDogsRequestModel
        {
            Data = request.SubmissionData,
            Options = new CDogsOptionsModel
            {
                ReportName = request.TemplateKey,
                ConvertTo = "pdf",
                Overwrite = true,
            },
            Template = new CDogsTemplateModel
            {
                Content = templateBase64,
                EncodingType = "base64",
                FileType = "docx",
            },
        };

        var result = await _cdogsDelegate.GenerateReportAsync(cdogsRequest, ct);

        if (result.ResultStatus == ReportResultType.Error)
            return BadRequest(result.ResultError);

        var bytes = Convert.FromBase64String(result.ResourcePayload!.Data);
        return File(bytes, "application/pdf", result.ResourcePayload.FileName);
    }

    /// <summary>
    /// Low-level endpoint: caller provides the full CDogs request (template + data).
    /// Used for testing and Swagger exploration.
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate(
        [FromBody] CDogsRequestModel request,
        CancellationToken ct
    )
    {
        var result = await _cdogsDelegate.GenerateReportAsync(request, ct);

        if (result.ResultStatus == ReportResultType.Error)
            return BadRequest(result.ResultError);

        return Ok(result.ResourcePayload);
    }
}
