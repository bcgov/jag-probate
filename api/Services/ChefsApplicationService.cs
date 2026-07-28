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
            !_options.Forms.TryGetValue(formKey, out var formOptions)
            || string.IsNullOrWhiteSpace(formOptions?.FormId)
        )
            throw new InvalidOperationException($"Form key '{formKey}' is not configured.");

        _logger.LogInformation(
            "Fetching CHEFS applications for form key {FormKey}",
            LogSanitizer.Sanitize(formKey)
        );

        List<ChefsSubmissionSummary> response;
        try
        {
            response = await _chefsApi.GetSubmissionsAsync(formOptions.FormId, cancellationToken);
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
            !_options.Forms.TryGetValue(formKey, out var formOptions)
            || string.IsNullOrWhiteSpace(formOptions?.FormId)
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
                formOptions.FormId,
                new ChefsAuthTokenRequest { FormId = formOptions.FormId },
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

        _logger.LogInformation(
            "Retrieved auth token for form key {FormKey}",
            LogSanitizer.Sanitize(formKey)
        );

        return new ChefsAuthTokenDto
        {
            Token = response.Token,
            FormId = formOptions.FormId,
            BaseUrl = _options.BaseUrl.TrimEnd('/'),
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<SidebarStepDto> GetSidebarStructure()
    {
        var steps = _options
            .Forms.Where(kv => !string.IsNullOrWhiteSpace(kv.Value.Title))
            .Select(kv =>
            {
                var (formKey, formOptions) = kv;

                var orderedSubsteps = formOptions
                    .Children.Where(s =>
                        !string.IsNullOrWhiteSpace(s.Key) && !string.IsNullOrWhiteSpace(s.Title)
                    )
                    .Select(s =>
                    {
                        return new SidebarSubstepDto
                        {
                            Key = s.Key,
                            Order = s.Order ?? int.MaxValue,
                            Title = s.Title,
                            Icon = s.Icon,
                            Disabled = s.Disabled,
                        };
                    })
                    .OrderBy(s => s.Order)
                    .ToList();

                return new SidebarStepDto
                {
                    Key = formKey,
                    Order = formOptions.Order ?? int.MaxValue,
                    Title = formOptions.Title,
                    Icon = formOptions.Icon,
                    Children = orderedSubsteps,
                };
            })
            .OrderBy(s => s.Order)
            .ToList();

        return steps;
    }
}
