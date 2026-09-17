using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Probate.Api.Models;
using Probate.Api.Services;

namespace Probate.Api.Infrastructure.WeasyPrint;

public class WeasyPrintPDFService : IPDFGenerationService
{
    private static readonly ActivitySource ActivitySource = new(
        typeof(WeasyPrintPDFService).FullName!
    );

    private readonly IWeasyPrintApi _weasyPrintApi;
    private readonly ILogger<WeasyPrintPDFService> _logger;

    public WeasyPrintPDFService(IWeasyPrintApi weasyPrintApi, ILogger<WeasyPrintPDFService> logger)
    {
        _weasyPrintApi = weasyPrintApi;
        _logger = logger;
    }

    public async Task<ReportRequestResult<ReportModel>> GeneratePdfAsync(
        PdfGenerationRequest request,
        CancellationToken ct = default
    )
    {
        using var activity = ActivitySource.StartActivity();
        var result = new ReportRequestResult<ReportModel> { ResultStatus = ReportResultType.Error };

        try
        {
            var response = await _weasyPrintApi.GeneratePdfAsync(
                request.Documents,
                request.FileName,
                ct
            );
            activity?.AddBaggage("ResponseStatusCode", response.StatusCode.ToString());

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "WeasyPrint PDF generation failed. StatusCode: {StatusCode}. Body: {Body}",
                    response.StatusCode,
                    errorBody
                );
                result.ResultError = new ReportRequestResultError
                {
                    ReportResultMessage =
                        $"WeasyPrint returned HTTP {(int)response.StatusCode}: {errorBody}",
                    ErrorCode = "CommunicationInternal_PDFGeneration",
                };
                return result;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            result.ResultStatus = ReportResultType.Success;
            result.ResourcePayload = new ReportModel
            {
                Data = Convert.ToBase64String(buffer.ToArray()),
                FileName = request.FileName,
            };
            result.TotalResultCount = 1;
            return result;
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "WeasyPrint request timed out");
            result.ResultError = new ReportRequestResultError
            {
                ReportResultMessage = "WeasyPrint request timed out.",
                ErrorCode = "CommunicationInternal_PDFGeneration",
            };
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception generating PDF using WeasyPrint");
            result.ResultError = new ReportRequestResultError
            {
                ReportResultMessage = $"Exception generating PDF using WeasyPrint: {ex.Message}",
                ErrorCode = "CommunicationInternal_PDFGeneration",
            };
            return result;
        }
    }
}
