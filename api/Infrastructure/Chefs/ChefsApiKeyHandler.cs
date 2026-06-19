using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Probate.Api.Options;

namespace Probate.Api.Infrastructure.Chefs;

/// <summary>
/// Adds the CHEFS api-key header to every request. We use api-key only (auth-token is not used).
/// form-id is sent per request by the Refit method (path + header).
/// For auth token requests, uses Basic Authentication with formId:apiKey.
/// </summary>
public class ChefsApiKeyHandler : DelegatingHandler
{
    private readonly ChefsOptions _options;

    public ChefsApiKeyHandler(IOptions<ChefsOptions> options)
    {
        _options = options.Value;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var path = request.RequestUri?.AbsolutePath ?? "";

        string? formId = null;

        // Submissions endpoint: /app/api/v1/forms/{formId}/submissions
        if (path.StartsWith("/app/api/v1/forms/", StringComparison.OrdinalIgnoreCase))
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (
                segments.Length >= 5
                && segments[3].Equals("forms", StringComparison.OrdinalIgnoreCase)
            )
            {
                formId = segments[4];
            }
        }
        // Auth token endpoint: app/gateway/v1/auth/token/forms/{formId}
        else if (
            path.StartsWith("/app/gateway/v1/auth/token/forms/", StringComparison.OrdinalIgnoreCase)
        )
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (
                segments.Length >= 7
                && segments[5].Equals("forms", StringComparison.OrdinalIgnoreCase)
            )
            {
                formId = segments[6];
            }
        }

        if (!string.IsNullOrEmpty(formId) && !string.IsNullOrEmpty(_options.ApiKey))
        {
            // Apply Basic Auth for dynamic formId
            var authValue = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{formId}:{_options.ApiKey}")
            );
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);

            // Optional debug logging
            System.Diagnostics.Debug.WriteLine(
                $"[CHEFS] Basic Auth applied for formId={formId}, URL={request.RequestUri}"
            );
        }
        else
        {
            // Fallback: apply API key header for other requests
            if (!string.IsNullOrEmpty(_options.ApiKey))
            {
                request.Headers.TryAddWithoutValidation("api-key", _options.ApiKey);
                System.Diagnostics.Debug.WriteLine(
                    $"[CHEFS] API Key header applied, URL={request.RequestUri}"
                );
            }
        }

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        // Optional debug logging for non-success responses
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            System.Diagnostics.Debug.WriteLine(
                $"[CHEFS] API Error ({response.StatusCode}) URL={request.RequestUri} Content={content.Substring(0, Math.Min(500, content.Length))}"
            );
        }

        return response;
    }
}
