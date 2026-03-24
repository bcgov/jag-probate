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
        if (
            !_options.Forms.TryGetValue(formKey, out var formGuid)
            || string.IsNullOrWhiteSpace(formGuid)
        )
            throw new InvalidOperationException($"Form key '{formKey}' is not configured.");

        _logger.LogInformation(
            "Fetching CHEFS applications for form key {FormKey}",
            LogSanitizer.Sanitize(formKey)
        );

        List<ChefsSubmissionSummary> response;
        try
        {
            response = await _chefsApi.GetSubmissionsAsync(formGuid, cancellationToken);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(
                ex,
                "CHEFS API error for form key {FormKey}: {StatusCode} {Content}",
                LogSanitizer.Sanitize(formKey),
                ex.StatusCode,
                ex.Content
            );
            var statusCode = (HttpStatusCode)ex.StatusCode;
            var message = !string.IsNullOrWhiteSpace(ex.Content)
                ? $"CHEFS API error: {ex.Content}"
                : $"CHEFS API returned {(int)ex.StatusCode} ({ex.StatusCode}).";
            throw new ChefsApiException(message, statusCode, ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "CHEFS API request failed for form key {FormKey}",
                LogSanitizer.Sanitize(formKey)
            );
            throw new ChefsApiException(
                "Unable to reach CHEFS API. Please try again later.",
                HttpStatusCode.BadGateway,
                ex
            );
        }
        catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("Request was cancelled.", ex, cancellationToken);
        }

        var applications = (response ?? new List<ChefsSubmissionSummary>())
            .Select(s => new ApplicationDto
            {
                Id = s.SubmissionId,
                ConfirmationId = s.ConfirmationId,
                Status = s.FormSubmissionStatusCode,
                CreatedAt = s.CreatedAt,
                CreatedBy = s.CreatedBy,
                UpdatedAt = s.UpdatedAt,
                UpdatedBy = s.UpdatedBy,
            })
            .ToList();

        _logger.LogInformation(
            "Retrieved {Count} applications for form key {FormKey}",
            applications.Count,
            LogSanitizer.Sanitize(formKey)
        );
        return applications;
    }

    /// <inheritdoc />
    public async Task<ChefsAuthTokenDto> GetAuthTokenAsync(
        string formKey,
        CancellationToken cancellationToken = default
    )
    {
        if (
            !_options.Forms.TryGetValue(formKey, out var formGuid)
            || string.IsNullOrWhiteSpace(formGuid)
        )
            throw new InvalidOperationException($"Form key '{formKey}' is not configured.");

        _logger.LogInformation(
            "Fetching CHEFS auth token for form key {FormKey}",
            LogSanitizer.Sanitize(formKey)
        );

        ChefsAuthTokenResponse response;
        try
        {
            response = await _chefsApi.GetAuthTokenAsync(
                formGuid,
                new ChefsAuthTokenRequest { FormId = formGuid },
                cancellationToken
            );
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(
                ex,
                "CHEFS API error for form key {FormKey}: {StatusCode} {Content}",
                LogSanitizer.Sanitize(formKey),
                ex.StatusCode,
                ex.Content
            );
            var statusCode = (HttpStatusCode)ex.StatusCode;
            var message = !string.IsNullOrWhiteSpace(ex.Content)
                ? $"CHEFS API error: {ex.Content}"
                : $"CHEFS API returned {(int)ex.StatusCode} ({ex.StatusCode}).";
            throw new ChefsApiException(message, statusCode, ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "CHEFS API request failed for form key {FormKey}",
                LogSanitizer.Sanitize(formKey)
            );
            throw new ChefsApiException(
                "Unable to reach CHEFS API. Please try again later.",
                HttpStatusCode.BadGateway,
                ex
            );
        }
        catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("Request was cancelled.", ex, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error fetching CHEFS auth token for form key {FormKey}. Exception type: {ExceptionType}",
                LogSanitizer.Sanitize(formKey),
                ex.GetType().Name
            );
            throw;
        }

        _logger.LogInformation(
            "Retrieved auth token for form key {FormKey}",
            LogSanitizer.Sanitize(formKey)
        );

        return new ChefsAuthTokenDto
        {
            Token = response.Token,
            FormId = formGuid,
            BaseUrl = _options.BaseUrl.TrimEnd('/'),
        };
    }
}
