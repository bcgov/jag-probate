using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
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

        // Review the IChefsApi interface to see which endpoints require formId in the path.
        var formIdMatch = Regex.Match(
            path,
            @"^(?:/api/v1/forms/|/gateway/v1/auth/token/forms/)([^/]+)",
            RegexOptions.IgnoreCase
        );

        string? formId = formIdMatch.Success ? formIdMatch.Groups[1].Value : null;

        // When the Form ID does not resolve to a API Key, we'll allow CHEFS to handle failures.
        if (
            !string.IsNullOrEmpty(formId) && _options.Forms.TryGetValue(formId, out var formOptions)
        )
        {
            var apiKey = formOptions.ApiKey;
            // Apply Basic Auth for dynamic formId
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{formId}:{apiKey}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);

            // Optional debug logging
            System.Diagnostics.Debug.WriteLine(
                $"[CHEFS] Basic Auth applied for formId={formId}, URL={request.RequestUri}"
            );
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
