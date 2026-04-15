namespace Probate.Api.Infrastructure.CDogs
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Probate.Api.Models;
    using Probate.Api.Models.CDogs;

    /// <summary>
    /// The CDOGS report generator delegate.
    /// </summary>
    public class CDogsDelegate : ICDogsDelegate
    {
        private readonly ICDogsApi cdogsApi;
        private readonly ILogger<CDogsDelegate> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CDogsDelegate"/> class.
        /// </summary>
        /// <param name="logger">The injected Logger Provider.</param>
        /// <param name="cdogsApi">The injected Refit API.</param>
        public CDogsDelegate(ILogger<CDogsDelegate> logger, ICDogsApi cdogsApi)
        {
            this.logger = logger;
            this.cdogsApi = cdogsApi;
        }

        private static ActivitySource ActivitySource { get; } = new(typeof(CDogsDelegate).FullName);

        /// <inheritdoc/>
        public async Task<ReportRequestResult<ReportModel>> GenerateReportAsync(
            CDogsRequestModel request,
            CancellationToken ct = default
        )
        {
            using Activity? activity = ActivitySource.StartActivity();

            ReportRequestResult<ReportModel> retVal = new()
            {
                ResultStatus = ReportResultType.Error,
            };

            try
            {
                logger.LogDebug("Generating report using CDOGS");
                HttpResponseMessage response = await this.cdogsApi.GenerateDocumentAsync(
                    request,
                    ct
                );

                activity?.AddBaggage("ResponseStatusCode", response.StatusCode.ToString());

                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync(ct);

                    logger.LogWarning(
                        "CDOGS report generation failed. StatusCode: {StatusCode}. Body: {Body}",
                        response.StatusCode,
                        errorBody
                    );

                    retVal.ResultError = new ReportRequestResultError
                    {
                        ReportResultMessage =
                            $"CDOGS returned HTTP {(int)response.StatusCode}: {errorBody}",
                        ErrorCode = ErrorTranslator.ServiceError(
                            ErrorType.CommunicationInternal,
                            ServiceType.CDogs
                        ),
                    };

                    return retVal;
                }

                await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
                using MemoryStream ms = new();

                await stream.CopyToAsync(ms, ct);

                retVal.ResultStatus = ReportResultType.Success;
                retVal.ResourcePayload = new ReportModel
                {
                    Data = Convert.ToBase64String(ms.ToArray()),
                    FileName = $"{request.Options.ReportName}.{request.Options.ConvertTo}",
                };
                retVal.TotalResultCount = 1;

                return retVal;
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(ex, "CDOGS request timed out");

                retVal.ResultError = new ReportRequestResultError
                {
                    ReportResultMessage = "CDOGS request timed out.",
                    ErrorCode = ErrorTranslator.ServiceError(
                        ErrorType.CommunicationInternal,
                        ServiceType.CDogs
                    ),
                };

                return retVal;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Exception generating report using CDOGS");

                retVal.ResultError = new ReportRequestResultError
                {
                    ReportResultMessage = $"Exception generating report using CDOGS: {ex.Message}",
                    ErrorCode = ErrorTranslator.ServiceError(
                        ErrorType.CommunicationInternal,
                        ServiceType.CDogs
                    ),
                };

                return retVal;
            }
        }
    }
}
