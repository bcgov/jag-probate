using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Probate.Api.Helpers.Exceptions;
using Probate.Api.Infrastructure.Chefs;
using Probate.Api.Models;
using Probate.Api.Options;
using Refit;

namespace Probate.Api.Services;

/// <summary>
/// Retrieves CHEFS applications (submissions) for the configured form. Validates formId against Chefs:FormId.
/// </summary>
public class ChefsApplicationService : IChefsApplicationService
{
    private readonly IChefsApi _chefsApi;
    private readonly ChefsOptions _options;
    private readonly ILogger<ChefsApplicationService> _logger;

    public ChefsApplicationService(
        IChefsApi chefsApi,
        IOptions<ChefsOptions> options,
        ILogger<ChefsApplicationService> logger
    )
    {
        _chefsApi = chefsApi;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApplicationDto>> GetApplicationsAsync(
        string formId,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Fetching CHEFS applications for form {FormId}", formId);

        ChefsSubmissionsResponse response;
        try
        {
            response = await _chefsApi.GetSubmissionsAsync(formId, cancellationToken);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, "CHEFS API error for form {FormId}: {StatusCode} {Content}", formId, ex.StatusCode, ex.Content);
            var statusCode = (HttpStatusCode)ex.StatusCode;
            var message = !string.IsNullOrWhiteSpace(ex.Content)
                ? $"CHEFS API error: {ex.Content}"
                : $"CHEFS API returned {(int)ex.StatusCode} ({ex.StatusCode}).";
            throw new ChefsApiException(message, statusCode, ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "CHEFS API request failed for form {FormId}", formId);
            throw new ChefsApiException("Unable to reach CHEFS API. Please try again later.", HttpStatusCode.BadGateway, ex);
        }
        catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("Request was cancelled.", ex, cancellationToken);
        }

        var applications = (response.Submissions ?? new List<ChefsSubmissionSummary>())
            .Select(s => new ApplicationDto
            {
                Id = s.Id,
                FormId = s.FormId,
                Status = s.Status,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
            })
            .ToList();

        _logger.LogInformation("Retrieved {Count} applications for form {FormId}", applications.Count, formId);
        return applications;
    }
}
