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
        // Check if this is an auth token request
        if (request.RequestUri?.PathAndQuery.Contains("/gateway/v1/auth/token/forms/") == true)
        {
            // Extract formId from the path
            var segments = request.RequestUri.Segments;
            var formId = segments.Length > 0 ? segments[^1].TrimEnd('/') : null;

            if (!string.IsNullOrEmpty(formId) && !string.IsNullOrEmpty(_options.ApiKey))
            {
                // Use Basic Auth: formId:apiKey
                var authValue = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{formId}:{_options.ApiKey}")
                );
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);

                // Log for debugging
                System.Diagnostics.Debug.WriteLine(
                    $"CHEFS Auth Token Request: {request.RequestUri}"
                );
                System.Diagnostics.Debug.WriteLine($"Using Basic Auth with formId: {formId}");
            }
        }
        else
        {
            // For other requests, use api-key header
            if (!string.IsNullOrEmpty(_options.ApiKey))
            {
                request.Headers.TryAddWithoutValidation("api-key", _options.ApiKey);
            }
        }

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        // Log response for debugging
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            System.Diagnostics.Debug.WriteLine(
                $"CHEFS API Error Response ({response.StatusCode}): {content.Substring(0, Math.Min(500, content.Length))}"
            );
        }

        return response;
    }
}
