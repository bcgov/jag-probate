using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Probate.Api.Helpers;
using Probate.Api.Helpers.Exceptions;
using Probate.Api.Infrastructure.Chefs;
using Probate.Api.Models;
using Probate.Api.Options;
using Refit;

namespace Probate.Api.Services;

/// <summary>
/// Retrieves CHEFS applications (submissions) for a form identified by a logical key.
/// Resolves the logical key to the actual CHEFS form GUID via Chefs:Forms config; the GUID is never exposed to callers.
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
        string formKey,
        CancellationToken cancellationToken = default
    )
    {
        if (!_options.Forms.TryGetValue(formKey, out var formGuid) || string.IsNullOrWhiteSpace(formGuid))
            throw new InvalidOperationException($"Form key '{formKey}' is not configured.");

        _logger.LogInformation("Fetching CHEFS applications for form key {FormKey}", LogSanitizer.Sanitize(formKey));

        ChefsSubmissionsResponse response;
        try
        {
            response = await _chefsApi.GetSubmissionsAsync(formGuid, cancellationToken);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, "CHEFS API error for form key {FormKey}: {StatusCode} {Content}", LogSanitizer.Sanitize(formKey), ex.StatusCode, ex.Content);
            var statusCode = (HttpStatusCode)ex.StatusCode;
            var message = !string.IsNullOrWhiteSpace(ex.Content)
                ? $"CHEFS API error: {ex.Content}"
                : $"CHEFS API returned {(int)ex.StatusCode} ({ex.StatusCode}).";
            throw new ChefsApiException(message, statusCode, ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "CHEFS API request failed for form key {FormKey}", LogSanitizer.Sanitize(formKey));
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

        _logger.LogInformation("Retrieved {Count} applications for form key {FormKey}", applications.Count, LogSanitizer.Sanitize(formKey));
        return applications;
    }
}
