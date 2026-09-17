using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Probate.Api.Helpers;
using Probate.Api.Models;
using Probate.Api.Services;

namespace Probate.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportController : ControllerBase
{
    private static readonly Regex SafeKeyPattern = new(@"^[a-zA-Z0-9\-]+$", RegexOptions.Compiled);

    private readonly IPDFGenerationService _pdfGenerationService;
    private readonly ITemplateService _templateService;

    public ReportController(
        IPDFGenerationService pdfGenerationService,
        ITemplateService templateService
    )
    {
        _pdfGenerationService = pdfGenerationService;
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

        var data = request.TemplateKey.ToUpperInvariant() switch
        {
            "PGT" => SubmissionEnricher.EnrichPGT(request.SubmissionData),
            "P9" => SubmissionEnricher.EnrichP9(request.SubmissionData),
            _ => request.SubmissionData,
        };

        var result = await _pdfGenerationService.GeneratePdfAsync(
            new PdfGenerationRequest
            {
                Documents = _templateService.RenderTemplates(request.TemplateKey, data),
                FileName = $"{request.TemplateKey}.pdf",
            },
            ct
        );

        if (result.ResultStatus == ReportResultType.Error)
            return BadRequest(result.ResultError);

        var bytes = Convert.FromBase64String(result.ResourcePayload!.Data);
        return File(bytes, "application/pdf", result.ResourcePayload.FileName);
    }
}
